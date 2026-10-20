using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text.Json;

if (args.Length != 3 && args.Length != 4)
    throw new ArgumentException("Usage: RestoreDownloads <original-2.99.exe> <built-source.exe> <output.exe> [report.json]");
var originalPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[2]);
if (originalPath.Equals(outputPath, StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Output must differ from the original. Back up before installing.");
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));
var frameworkDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319");
resolver.AddSearchDirectory(frameworkDirectory);
resolver.AddSearchDirectory(Path.Combine(frameworkDirectory, "WPF"));
using var original = ModuleDefinition.ReadModule(originalPath, new ReaderParameters
{
    MetadataImporterProvider = new LocalImporterProvider(),
    AssemblyResolver = resolver
});
using var donor = ModuleDefinition.ReadModule(args[1]);
if (original.Assembly.Name.Version != new Version(2, 99, 0, 0) ||
    donor.Assembly.Name.Version != original.Assembly.Name.Version)
    throw new InvalidDataException("Only matching 2.99.0.0 builds are supported.");
var originalTypes = original.GetTypes().ToDictionary(t => t.FullName);
var donorTypes = donor.GetTypes().ToDictionary(t => t.FullName);
var originalMethods = original.GetTypes().SelectMany(t => t.Methods).ToDictionary(m => m.FullName);
var originalFields = original.GetTypes().SelectMany(t => t.Fields).ToDictionary(f => f.FullName);
var before = originalMethods.ToDictionary(kv => kv.Key, kv => Fingerprint(kv.Value));
var changed = new HashSet<string>();
var copied = new List<string>();
var verifiedHelpers = new HashSet<string>();
var secretBefore = originalTypes["PCL.ModSecret"].Methods.ToDictionary(m => m.FullName, Fingerprint);

foreach (var (typeName, methodName) in new[]
{
    ("PCL.PageDownloadInstall", "LoaderInit"),
    ("PCL.ModModpack", "ModpackInstall"),
    ("PCL.PageDownloadResourceDetail", "InitializeStackResourceVersions")
})
{
    foreach (var source in donorTypes[typeName].Methods.Where(m => m.Name == methodName))
    {
        var target = originalMethods[source.FullName];
        CloneBody(source, target);
        copied.Add(source.FullName);
        changed.Add(target.FullName);
    }
}

var modify = originalTypes["PCL.PageDownloadModify"].Methods.Single(m => m.Name == "LoadInstanceFinished");
var visibilitySites = modify.Body.Instructions.Where(i =>
    i.Operand is MethodReference m && m.Name == "get_CardMinecraft").ToArray();
if (visibilitySites.Length != 1 || visibilitySites[0].Next.OpCode != OpCodes.Ldc_I4_2 ||
    visibilitySites[0].Next.Next.Operand is not MethodReference visibility || visibility.Name != "set_Visibility")
    throw new InvalidDataException("Unexpected Minecraft card visibility implementation.");
visibilitySites[0].Next.OpCode = OpCodes.Ldc_I4_0;
changed.Add(modify.FullName);
originalTypes["PCL.ModSecret"].Fields.Single(f => f.Name == "RestrictMcDownload").Constant = false;

// Distinguish the personal fork in visible version labels without replacing private code.
var versionField = originalTypes["PCL.ModBase"].Fields.Single(f => f.Name == "VersionDisplay");
var oldVersion = (string)versionField.Constant;
var newVersion = oldVersion + " · Lacr1me 自用版";
versionField.Constant = newVersion;
foreach (var method in originalMethods.Values.Where(m => m.HasBody))
    foreach (var instruction in method.Body.Instructions)
        if (instruction.OpCode == OpCodes.Ldstr && Equals(instruction.Operand, oldVersion) &&
            method.DeclaringType.FullName != "PCL.ModSecret")
        {
            instruction.Operand = newVersion;
            changed.Add(method.FullName);
        }
foreach (var attribute in original.Assembly.CustomAttributes.Where(a =>
    a.AttributeType.Name is "AssemblyTitleAttribute" or "AssemblyProductAttribute"))
    attribute.ConstructorArguments[0] = new CustomAttributeArgument(original.TypeSystem.String, "PCL Lacr1me 自用版");

foreach (var (name, fingerprint) in secretBefore)
    if (Fingerprint(originalMethods[name]) != fingerprint)
        throw new InvalidDataException("Private implementation changed: " + name);
foreach (var (name, fingerprint) in before)
    if (!changed.Contains(name) && Fingerprint(originalMethods[name]) != fingerprint)
        throw new InvalidDataException("Unrelated method changed: " + name);
foreach (var name in copied)
    if (originalMethods[name].Body.Instructions.Any(i => i.Operand is string s && s.Contains("暂时不再提供 MC 下载功能")))
        throw new InvalidDataException("Restriction remains in restored method: " + name);
if (original.AssemblyReferences.Any(a => a.Name == original.Assembly.Name.Name))
    throw new InvalidDataException("Unexpected self assembly reference.");

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
original.Write(outputPath);
using var written = ModuleDefinition.ReadModule(outputPath);
var writtenMethods = written.GetTypes().SelectMany(t => t.Methods).ToDictionary(m => m.FullName);
foreach (var (name, fingerprint) in before)
    if (!changed.Contains(name) && Fingerprint(writtenMethods[name]) != fingerprint)
        throw new InvalidDataException("Round-trip changed unrelated method: " + name);
foreach (var resource in original.Resources.OfType<EmbeddedResource>())
{
    var result = written.Resources.OfType<EmbeddedResource>().Single(r => r.Name == resource.Name);
    if (!resource.GetResourceData().SequenceEqual(result.GetResourceData()))
        throw new InvalidDataException("Embedded resource changed: " + resource.Name);
}
var report = new
{
    baseVersion = written.Assembly.Name.Version.ToString(),
    restoredMethods = copied,
    minecraftCardRestored = true,
    verifiedCompilerHelpers = verifiedHelpers.Count,
    privateImplementationUnchanged = secretBefore.Count,
    unrelatedMethodsUnchanged = before.Count - changed.Count,
    embeddedResourcesPreserved = original.Resources.Count,
    modifiedMethods = changed.Order().ToArray(),
    output = outputPath
};
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
if (args.Length == 4) File.WriteAllText(args[3], json);
Console.WriteLine(json);

void CloneBody(MethodDefinition source, MethodDefinition target)
{
    var body = new MethodBody(target) { InitLocals = source.Body.InitLocals, MaxStackSize = source.Body.MaxStackSize };
    var locals = source.Body.Variables.ToDictionary(v => v, v => new VariableDefinition(original.ImportReference(v.VariableType, target)));
    foreach (var local in source.Body.Variables) body.Variables.Add(locals[local]);
    var instructions = source.Body.Instructions.ToDictionary(i => i, _ => Instruction.Create(OpCodes.Nop));
    foreach (var instruction in source.Body.Instructions)
    {
        var copy = instructions[instruction];
        copy.OpCode = instruction.OpCode;
        copy.Operand = instruction.Operand switch
        {
            null => null,
            Instruction branch => instructions[branch],
            Instruction[] branches => branches.Select(b => instructions[b]).ToArray(),
            VariableDefinition local => locals[local],
            ParameterDefinition parameter => target.Parameters[parameter.Index],
            MethodReference method => ImportMethod(method, target),
            FieldReference field => ImportField(field, target),
            TypeReference type => original.ImportReference(type, target),
            CallSite => throw new NotSupportedException("Unexpected calli."),
            string s when s.EndsWith(".vb") && s.Contains("Plain Craft Launcher 2") =>
                s[s.IndexOf("Plain Craft Launcher 2", StringComparison.Ordinal)..],
            var value => value
        };
        body.Instructions.Add(copy);
    }
    Instruction? Map(Instruction? i) => i == null ? null : instructions[i];
    foreach (var handler in source.Body.ExceptionHandlers)
        body.ExceptionHandlers.Add(new ExceptionHandler(handler.HandlerType)
        {
            TryStart = Map(handler.TryStart), TryEnd = Map(handler.TryEnd),
            HandlerStart = Map(handler.HandlerStart), HandlerEnd = Map(handler.HandlerEnd),
            FilterStart = Map(handler.FilterStart),
            CatchType = handler.CatchType == null ? null : original.ImportReference(handler.CatchType, target)
        });
    target.Body = body;
}

MethodReference ImportMethod(MethodReference method, MethodDefinition context)
{
    if (method is not GenericInstanceMethod && method.DeclaringType is not GenericInstanceType &&
        donorTypes.ContainsKey(method.DeclaringType.FullName))
    {
        if (!originalMethods.TryGetValue(method.FullName, out var existing))
            throw new InvalidDataException("Missing original helper: " + method.FullName);
        if ((method.Name.StartsWith("_Lambda$", StringComparison.Ordinal) ||
             method.DeclaringType.Name.StartsWith("_Closure$", StringComparison.Ordinal)) &&
            verifiedHelpers.Add(method.FullName))
        {
            var helper = donorTypes[method.DeclaringType.FullName].Methods.Single(m => m.FullName == method.FullName);
            if (Fingerprint(existing) != Fingerprint(helper))
                throw new InvalidDataException("Compiler helper differs: " + method.FullName);
        }
        return existing;
    }
    return original.ImportReference(method, context);
}

FieldReference ImportField(FieldReference field, MethodDefinition context)
{
    if (field.DeclaringType is not GenericInstanceType && donorTypes.ContainsKey(field.DeclaringType.FullName))
        return originalFields.TryGetValue(field.FullName, out var existing) ? existing :
            throw new InvalidDataException("Missing original field: " + field.FullName);
    return original.ImportReference(field, context);
}

static string Fingerprint(MethodDefinition method)
{
    if (!method.HasBody) return "";
    var body = method.Body;
    string Operand(object? operand) => operand switch
    {
        null => "",
        Instruction i => "#" + body.Instructions.IndexOf(i),
        Instruction[] list => string.Join(",", list.Select(i => body.Instructions.IndexOf(i))),
        VariableDefinition v => "local:" + v.Index,
        ParameterDefinition p => "arg:" + p.Index,
        MemberReference m => m.FullName,
        string s when s.EndsWith(".vb") && s.Contains("Plain Craft Launcher 2") => s[s.IndexOf("Plain Craft Launcher 2", StringComparison.Ordinal)..],
        _ => Convert.ToString(operand, System.Globalization.CultureInfo.InvariantCulture) ?? ""
    };
    return string.Join("\n", body.Variables.Select(v => v.VariableType.FullName)) + "\n" +
        string.Join("\n", body.Instructions.Select(i => i.OpCode.Code + " " + Operand(i.Operand))) + "\n" +
        string.Join("\n", body.ExceptionHandlers.Select(h => $"{h.HandlerType}|{Operand(h.TryStart)}|{Operand(h.TryEnd)}|{Operand(h.HandlerStart)}|{Operand(h.HandlerEnd)}|{Operand(h.FilterStart)}|{h.CatchType?.FullName}"));
}

sealed class LocalImporterProvider : IMetadataImporterProvider
{
    public IMetadataImporter GetMetadataImporter(ModuleDefinition module) => new LocalImporter(module);
}
sealed class LocalImporter(ModuleDefinition target) : DefaultMetadataImporter(target)
{
    protected override IMetadataScope ImportScope(TypeReference type) =>
        type.Scope is ModuleDefinition source && source.Assembly.Name.Name == module.Assembly.Name.Name
            ? module : base.ImportScope(type);
}
