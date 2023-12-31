using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace RreparedMinimalApiJsonSerializerOptions;

public static class MinimalApiJsonSerializerOptions
{
    public static JsonSerializerOptions Default { get; } = new JsonSerializerOptions()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { info => ThrowNullableRequired(info) }
        }
    };

    internal static NullabilityInfoContext NullabilityInfoContext { get; } = new();

    internal static void ThrowNullableRequired(JsonTypeInfo jsonTypeInfo, Func<string, Exception>? throwFunc = null)
    {
        if (jsonTypeInfo.Kind != JsonTypeInfoKind.Object) return;
        throwFunc ??= (string prop) => new JsonException($"Not allow null to property: {prop}");

        foreach (var property in jsonTypeInfo.Properties)
        {
            // https://stackoverflow.com/questions/74491487/throw-exception-when-missing-not-nullable-value-in-system-text-json
            if (property is { AttributeProvider : { } att, PropertyType.IsValueType: false, Set: { } setter })
            {
                var propertyInfo = (att as PropertyInfo);

                property.Set = (obj, val) =>
                {
                    if (val is null)
                    {
                        if(obj.GetType()?.GetRuntimeProperty(property.Name)?.GetValue(obj) is null)
                        { // 자동 구현 속성 초기값이 없다

                            var nullabilityInfo = NullabilityInfoContext.Create(property.AttributeProvider is { } PropertyInfo v);

                            if (nullabilityInfo.WriteState is NullabilityState.NotNull)
                            {
                                throw throwFunc(property.Name);
                            }
                            else
                            {
                                setter(obj, val);
                            }
                        }
                    }
                    else
                    {
                        setter(obj, val);
                    }
                };
            }
        }
    }
}
