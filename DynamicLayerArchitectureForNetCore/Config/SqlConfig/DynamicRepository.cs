using System.Reflection;
using System.Reflection.Emit;
using DynamicLayerArchitectureForNetCore.CustomAttributes;

namespace DynamicLayerArchitectureForNetCore.Config.SqlConfig;

public static class DynamicRepository
{
    public static object? CreateRepository(Type type, IServiceProvider provider)
    {
        var moduleBuilder = CreateModule();
        var typeBuilder =
            moduleBuilder.DefineType(type.Name + "Proxy", TypeAttributes.Public | TypeAttributes.Class);
        //field
        var fieldBuilder = typeBuilder.DefineField("_dapper", typeof(DapperLogger), FieldAttributes.Private);
        CreateConstructor(typeBuilder, fieldBuilder);
        typeBuilder.AddInterfaceImplementation(type);
        
        //method
        CreateMethod(typeBuilder, fieldBuilder, type);

        typeBuilder.CreateType();

        return Activator.CreateInstance(typeBuilder.CreateType() ?? throw new InvalidOperationException(),
            provider.GetService(typeof(DapperLogger)));
    }

    private static ModuleBuilder CreateModule()
    {
        var assembly = new AssemblyName("DynamicRepository");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assembly, AssemblyBuilderAccess.RunAndCollect);

        var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicRepository");
        return moduleBuilder;
    }

    private static void CreateConstructor(TypeBuilder typeBuilder, FieldInfo fieldBuilder)
    {
        var constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName |
            MethodAttributes.SpecialName, CallingConventions.HasThis, new[] { typeof(DapperLogger) });
        var constructorInfo = typeof(object).GetConstructor(Type.EmptyTypes);
        var ilGenerator = constructorBuilder.GetILGenerator();
        ilGenerator.Emit(OpCodes.Ldarg_0);
        ilGenerator.Emit(OpCodes.Call, constructorInfo ?? throw new InvalidOperationException());
        ilGenerator.Emit(OpCodes.Nop);
        ilGenerator.Emit(OpCodes.Nop);
            
        ilGenerator.Emit(OpCodes.Ldarg_0);
        ilGenerator.Emit(OpCodes.Ldarg_S, 1);
        ilGenerator.Emit(OpCodes.Box, fieldBuilder.FieldType);
        ilGenerator.Emit(OpCodes.Stfld, fieldBuilder);
        ilGenerator.Emit(OpCodes.Ret);
    }

    private static void CreateMethod(TypeBuilder typeBuilder, FieldInfo fieldBuilder, Type type)
    {
        //method
        foreach (var method in type.GetMethods())
        {
            var parameters = method.GetParameters();
            var methodBuilder = typeBuilder.DefineMethod(method.Name,
                MethodAttributes.Public | MethodAttributes.Virtual, method.ReturnType,
                parameters.Select(param => param.ParameterType).ToArray());

            var ilGenerator = methodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            var dictionaryParams = ilGenerator.DeclareLocal(typeof(Dictionary<string, object>));
            ilGenerator.Emit(OpCodes.Newobj,
                typeof(Dictionary<string, object>).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
            ilGenerator.Emit(OpCodes.Stloc, dictionaryParams);
            ilGenerator.Emit(OpCodes.Nop);
            var paramNameLocal = ilGenerator.DeclareLocal(typeof(string));
            var dictionaryMethod =
                dictionaryParams.LocalType.GetMethod("Add");

            for (short paramIndex = 0; paramIndex < parameters.Length; paramIndex++)
            {
                var param = parameters[paramIndex];
                methodBuilder.DefineParameter(paramIndex + 1, ParameterAttributes.In, param.Name);
                ilGenerator.Emit(OpCodes.Ldstr, param.Name ?? throw new InvalidOperationException());
                ilGenerator.Emit(OpCodes.Stloc, paramNameLocal);
                ilGenerator.Emit(OpCodes.Nop);
                ilGenerator.Emit(OpCodes.Ldloc, dictionaryParams);
                ilGenerator.Emit(OpCodes.Ldloc, paramNameLocal);
                ilGenerator.Emit(OpCodes.Ldarg_S, paramIndex + 1);
                ilGenerator.Emit(OpCodes.Box, param.ParameterType);
                ilGenerator.EmitCall(OpCodes.Callvirt, dictionaryMethod ?? throw new InvalidOperationException(), null);
                ilGenerator.Emit(OpCodes.Nop);
            }
            ilGenerator.Emit(OpCodes.Nop);

            var queryString = ilGenerator.DeclareLocal(typeof(string));
            if (method.GetCustomAttributes(typeof(QueryAttribute), true)[0] is QueryAttribute attribute)
            {
                ilGenerator.Emit(OpCodes.Ldstr, attribute.Query);
                ilGenerator.Emit(OpCodes.Stloc, queryString);
            }
            ilGenerator.Emit(OpCodes.Nop);

            ilGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            ilGenerator.Emit(OpCodes.Ldloc, dictionaryParams);
            ilGenerator.Emit(OpCodes.Ldloc, queryString);

            var types = new[] { typeof(DapperLogger), dictionaryParams.LocalType, queryString.LocalType };
            var executeQuery = typeof(SqlExecuteQuery)
                .GetMethod("Execute", 
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance, 
                    null, 
                    types.ToArray(), 
                    null)
                ?.MakeGenericMethod(method.ReturnType);
                
            ilGenerator.Emit(OpCodes.Call, executeQuery ?? throw new InvalidOperationException());
            ilGenerator.Emit(OpCodes.Ret);
                
            var implementMethod = type.GetMethod(method.Name);
            typeBuilder.DefineMethodOverride(methodBuilder,
                implementMethod ?? throw new InvalidOperationException());
        }
    }
}