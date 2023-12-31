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
        Converters = { new NullToDefaultConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { ThrowNullableRequired }
        }
    };

    internal static NullabilityInfoContext NullabilityInfoContext { get; } = new();

    internal static void ThrowNullableRequired(JsonTypeInfo jsonTypeInfo, Func<string PropertyName, out Exception>? throwFunc)
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

                            var nullabilityInfo = NullabilityInfoContext.Create(property.AttributeProvider as PropertyInfo);

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

public class NullToDefaultConverter : DefaultConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsValueType && Nullable.GetUnderlyingType(typeToConvert) == null;
    protected sealed override bool HandleNull { get; } = true;
    protected override T? Read<T>(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions modifiedOptions) where T : default =>
        reader.TokenType switch
        {
            JsonTokenType.Null => default,
            _ => base.Read<T>(ref reader, typeToConvert, modifiedOptions),
        };
}

public abstract class DefaultConverterFactory : JsonConverterFactory
{
    // Adapted from this answer https://stackoverflow.com/a/65430421/3744182
    // To https://stackoverflow.com/questions/65430420/how-to-use-default-serialization-in-a-custom-system-text-json-jsonconverter
    class DefaultConverter<T> : JsonConverter<T>
    {
        readonly JsonSerializerOptions modifiedOptions;
        readonly DefaultConverterFactory factory;

        public DefaultConverter(JsonSerializerOptions modifiedOptions, DefaultConverterFactory factory) => (this.modifiedOptions, this.factory) = (modifiedOptions, factory);

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => factory.Write(writer, value, modifiedOptions);
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => factory.Read<T>(ref reader, typeToConvert, modifiedOptions);
        public override bool CanConvert(Type typeToConvert) => typeof(T).IsAssignableFrom(typeToConvert);
    }
    class NullHandlingDefaultConverter<T> : DefaultConverter<T>
    {
        public NullHandlingDefaultConverter(JsonSerializerOptions modifiedOptions, DefaultConverterFactory factory) : base(modifiedOptions, factory) { }
        public override bool HandleNull => true;
    }

    protected virtual JsonSerializerOptions ModifyOptions(JsonSerializerOptions options) =>
        options.CopyAndRemoveConverter(GetType());

    protected virtual T? Read<T>(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions modifiedOptions) =>
        (T?)JsonSerializer.Deserialize(ref reader, typeToConvert, modifiedOptions);

    protected virtual void Write<T>(Utf8JsonWriter writer, T value, JsonSerializerOptions modifiedOptions) =>
        JsonSerializer.Serialize(writer, value, modifiedOptions);

    public sealed override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = (HandleNull ? typeof(NullHandlingDefaultConverter<>) : typeof(DefaultConverter<>)).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType, new object[] { ModifyOptions(options), this })!;
    }

    protected virtual bool HandleNull { get; } = false;
}

public static class JsonSerializerExtensions
{
    public static JsonSerializerOptions CopyAndRemoveConverter(this JsonSerializerOptions options, Type converterType)
    {
        var copy = new JsonSerializerOptions(options);
        for (var i = copy.Converters.Count - 1; i >= 0; i--)
            if (copy.Converters[i].GetType() == converterType)
                copy.Converters.RemoveAt(i);
        return copy;
    }
}
