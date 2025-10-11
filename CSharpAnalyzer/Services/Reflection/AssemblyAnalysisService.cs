using System.Reflection;
using CSharpAnalyzer.Models.Reflection;

namespace CSharpAnalyzer.Services.Reflection;

/// <summary>
/// Service for analyzing loaded assemblies using reflection.
/// </summary>
public class AssemblyAnalysisService(AssemblyLoaderService loaderService)
{
    public AssemblyInfoResponse GetAssemblyInfo(string assemblyPath)
    {
        var response = new AssemblyInfoResponse { Success = false };

        try
        {
            // Ensure assembly is loaded
            LoadAssemblyResponse loadResult = loaderService.LoadAssembly(assemblyPath);
            if (!loadResult.Success)
            {
                response.Error = loadResult.Error;
                return response;
            }

            Assembly? assembly = loaderService.GetLoadedAssembly(assemblyPath);
            if (assembly == null)
            {
                response.Error = "Assembly was loaded but could not be retrieved";
                return response;
            }

            AssemblyName assemblyName = assembly.GetName();

            response.Success = true;
            response.Name = assemblyName.Name ?? string.Empty;
            response.FullName = assemblyName.FullName ?? string.Empty;
            response.Version = assemblyName.Version?.ToString() ?? string.Empty;
            response.Culture = assemblyName.CultureName ?? "neutral";
            
            byte[]? publicKeyToken = assemblyName.GetPublicKeyToken();
            response.PublicKeyToken = publicKeyToken != null && publicKeyToken.Length > 0
                ? Convert.ToHexStringLower(publicKeyToken)
                : "null";

            response.Location = assemblyPath;

            // Get target framework from attribute using CustomAttributeData
            CustomAttributeData? targetFrameworkAttr = assembly.CustomAttributes
                .FirstOrDefault(a => a.AttributeType.Name == "TargetFrameworkAttribute");
            
            if (targetFrameworkAttr != null && targetFrameworkAttr.ConstructorArguments.Count > 0)
            {
                response.TargetFramework = targetFrameworkAttr.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
            }


            // Get referenced assemblies
            response.ReferencedAssemblies = assembly.GetReferencedAssemblies()
                .Select(an => an.FullName)
                .ToList();
        }
        catch (Exception ex)
        {
            response.Error = $"Failed to get assembly info: {ex.Message}";
        }

        return response;
    }

    public ListTypesResponse ListTypes(ListTypesRequest request)
    {
        var response = new ListTypesResponse { Success = false };

        try
        {
            // Ensure assembly is loaded
            LoadAssemblyResponse loadResult = loaderService.LoadAssembly(request.AssemblyPath);
            if (!loadResult.Success)
            {
                response.Error = loadResult.Error;
                return response;
            }

            Assembly? assembly = loaderService.GetLoadedAssembly(request.AssemblyPath);
            if (assembly == null)
            {
                response.Error = "Assembly was loaded but could not be retrieved";
                return response;
            }

            // Get all types
            Type[] types = assembly.GetTypes();

            // Apply filters
            IEnumerable<Type> filteredTypes = types;

            if (request.PublicOnly)
            {
                filteredTypes = filteredTypes.Where(t => t.IsPublic || t.IsNestedPublic);
            }

            if (!string.IsNullOrEmpty(request.NamespaceFilter))
            {
                filteredTypes = filteredTypes.Where(t => 
                    t.Namespace != null && 
                    t.Namespace.StartsWith(request.NamespaceFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(request.TypeKindFilter))
            {
                filteredTypes = filteredTypes.Where(t => GetTypeKind(t).Equals(request.TypeKindFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Convert to TypeSummary
            foreach (Type type in filteredTypes)
            {
                response.Types.Add(new TypeSummary
                {
                    Name = type.Name,
                    FullName = type.FullName ?? type.Name,
                    Namespace = type.Namespace ?? string.Empty,
                    TypeKind = GetTypeKind(type),
                    IsPublic = type.IsPublic || type.IsNestedPublic,
                    IsAbstract = type.IsAbstract && !type.IsInterface,
                    IsSealed = type.IsSealed && !type.IsValueType,
                    IsGeneric = type.IsGenericType,
                    BaseType = type.BaseType?.FullName,
                    Interfaces = type.GetInterfaces().Select(i => i.FullName ?? i.Name).ToList()
                });
            }

            response.TotalCount = response.Types.Count;
            response.Success = true;
        }
        catch (Exception ex)
        {
            response.Error = $"Failed to list types: {ex.Message}";
        }

        return response;
    }

    private static string GetTypeKind(Type type)
    {
        if (type.IsInterface) return "Interface";
        if (type.IsEnum) return "Enum";
        if (type.IsValueType) return "Struct";
        if (typeof(Delegate).IsAssignableFrom(type)) return "Delegate";
        return "Class";
    }
}