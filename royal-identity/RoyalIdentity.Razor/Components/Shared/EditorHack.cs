using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

namespace RoyalIdentity.Razor.Components.Shared;

public class EditorHack<T> : Editor<T>
{
    [Parameter]
    public RenderFragment ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ChildContent(builder);
    }
}

public class CollectionEditor<TEnum, TValue> : ComponentBase
    where TEnum : class, IEnumerable<TValue>
{
    /// <summary>
    /// The value for the component.
    /// </summary>
    [Parameter] public TEnum Value { get; set; } = default!;

    /// <summary>
    /// An expression that represents the value for the component.
    /// </summary>
    [Parameter] public Expression<Func<TEnum>> ValueExpression { get; set; } = default!;

    /// <summary>
    /// A callback that gets invoked when the value changes.
    /// </summary>
    [Parameter] public EventCallback<TEnum> ValueChanged { get; set; } = default!;

    [Parameter] public RenderFragment<TValue> ChildContent { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (ValueExpression is null || Value is null)
        {
            throw new InvalidOperationException($"{GetType()} requires a value for the 'ValueExpression' " +
                "parameter. Normally this is provided automatically when using 'bind-Value'.");
        }

        base.OnParametersSet();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        int index = 0;
        foreach (var item in Value)
        {
            builder.OpenComponent<EditorHack<TValue>>(0);

            var expression = Expression.Lambda<Func<TValue>>(Expression.ArrayIndex(ValueExpression.Body, Expression.Constant(index)));
            builder.AddAttribute(1, nameof(EditorHack<TValue>.Value), item);
            builder.AddAttribute(2, nameof(EditorHack<TValue>.ValueExpression), expression);
            builder.AddAttribute(3, nameof(EditorHack<TValue>.ValueChanged), EventCallback.Factory.Create(this, (TValue value) =>
            {
                var valueArray = Value.ToList();
                valueArray[index] = value;
                TEnum? newValue = valueArray as TEnum;
                ValueChanged.InvokeAsync(newValue);
            }));

            builder.AddAttribute(4, nameof(EditorHack<TValue>.ChildContent), ChildContent(item));

            builder.CloseComponent();

            index++;
        }
    }
}

public static class ExpressionFormatter
{
    internal const int StackAllocBufferSize = 128;

    private delegate void CapturedValueFormatter(object closure, ref ReverseStringBuilder builder);

    private static readonly ConcurrentDictionary<MemberInfo, CapturedValueFormatter> s_capturedValueFormatterCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, MethodInfoData> s_methodInfoDataCache = new();

    public static void ClearCache()
    {
        s_capturedValueFormatterCache.Clear();
        s_methodInfoDataCache.Clear();
    }

    public static string FormatLambda(LambdaExpression expression)
    {
        return FormatLambda(expression, prefix: null);
    }

    public static string FormatLambda(LambdaExpression expression, string? prefix = null)
    {
        var builder = new ReverseStringBuilder(stackalloc char[StackAllocBufferSize]);
        var node = expression.Body;
        var wasLastExpressionMemberAccess = false;
        var wasLastExpressionIndexer = false;

        while (node is not null)
        {
            switch (node.NodeType)
            {
                case ExpressionType.Constant:
                    var constantExpression = (ConstantExpression)node;
                    node = null;
                    break;
                case ExpressionType.Call:
                    var methodCallExpression = (MethodCallExpression)node;

                    if (!IsSingleArgumentIndexer(methodCallExpression))
                    {
                        throw new InvalidOperationException("Method calls cannot be formatted.");
                    }

                    node = methodCallExpression.Object;
                    if (prefix != null && node is ConstantExpression)
                    {
                        break;
                    }

                    if (wasLastExpressionMemberAccess)
                    {
                        wasLastExpressionMemberAccess = false;
                        builder.InsertFront(".");
                    }
                    wasLastExpressionIndexer = true;

                    builder.InsertFront("]");
                    FormatIndexArgument(methodCallExpression.Arguments[0], ref builder);
                    builder.InsertFront("[");

                    break;

                case ExpressionType.ArrayIndex:
                    var binaryExpression = (BinaryExpression)node;
                    node = binaryExpression.Left;
                    if (prefix != null && node is ConstantExpression)
                    {
                        break;
                    }

                    if (wasLastExpressionMemberAccess)
                    {
                        wasLastExpressionMemberAccess = false;
                        builder.InsertFront(".");
                    }

                    builder.InsertFront("]");
                    FormatIndexArgument(binaryExpression.Right, ref builder);
                    builder.InsertFront("[");
                    wasLastExpressionIndexer = true;
                    break;

                case ExpressionType.MemberAccess:
                    var memberExpression = (MemberExpression)node;
                    node = memberExpression.Expression;
                    if (prefix != null && node is ConstantExpression)
                    {
                        break;
                    }

                    if (wasLastExpressionMemberAccess)
                    {
                        builder.InsertFront(".");
                    }
                    wasLastExpressionMemberAccess = true;
                    wasLastExpressionIndexer = false;

                    var name = memberExpression.Member.GetCustomAttribute<DataMemberAttribute>()?.Name ?? memberExpression.Member.Name;
                    builder.InsertFront(name);

                    break;

                default:
                    // Unsupported expression type.
                    node = null;
                    break;
            }
        }

        if (prefix != null)
        {
            if (!builder.Empty && !wasLastExpressionIndexer)
            {
                builder.InsertFront(".");
            }
            builder.InsertFront(prefix);
        }

        var result = builder.ToString();

        builder.Dispose();

        return result;
    }

    internal static bool IsSingleArgumentIndexer(Expression expression)
    {
        if (expression is not MethodCallExpression methodExpression || methodExpression.Arguments.Count != 1)
        {
            return false;
        }

        var methodInfoData = GetOrCreateMethodInfoData(methodExpression.Method);
        return methodInfoData.IsSingleArgumentIndexer;
    }

    private static MethodInfoData GetOrCreateMethodInfoData(MethodInfo methodInfo)
    {
        if (!s_methodInfoDataCache.TryGetValue(methodInfo, out var methodInfoData))
        {
            methodInfoData = GetMethodInfoData(methodInfo);
            s_methodInfoDataCache[methodInfo] = methodInfoData;
        }

        return methodInfoData;

        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The relevant members should be preserved since they were referenced in a LINQ expression")]
        static MethodInfoData GetMethodInfoData(MethodInfo methodInfo)
        {
            var declaringType = methodInfo.DeclaringType;
            if (declaringType is null)
            {
                return new(isSingleArgumentIndexer: false);
            }

            // Check whether GetDefaultMembers() (if present in CoreCLR) would return a member of this type. Compiler
            // names the indexer property, if any, in a generated [DefaultMember] attribute for the containing type.
            var defaultMember = declaringType.GetCustomAttribute<DefaultMemberAttribute>(inherit: true);
            if (defaultMember is null)
            {
                return new(isSingleArgumentIndexer: false);
            }

            // Find default property (the indexer) and confirm its getter is the method in this expression.
            var runtimeProperties = declaringType.GetRuntimeProperties();
            if (runtimeProperties is null)
            {
                return new(isSingleArgumentIndexer: false);
            }

            foreach (var property in runtimeProperties)
            {
                if (string.Equals(defaultMember.MemberName, property.Name, StringComparison.Ordinal) &&
                    property.GetMethod == methodInfo)
                {
                    return new(isSingleArgumentIndexer: true);
                }
            }

            return new(isSingleArgumentIndexer: false);
        }
    }

    private static void FormatIndexArgument(
        Expression indexExpression,
        ref ReverseStringBuilder builder)
    {
        switch (indexExpression)
        {
            case MemberExpression memberExpression when memberExpression.Expression is ConstantExpression constantExpression:
                FormatCapturedValue(memberExpression, constantExpression, ref builder);
                break;
            case ConstantExpression constantExpression:
                FormatConstantValue(constantExpression, ref builder);
                break;
            default:
                throw new InvalidOperationException($"Unable to evaluate index expressions of type '{indexExpression.GetType().Name}'.");
        }
    }

    internal static string FormatIndexArgument(
        Expression indexExpression)
    {
        var builder = new ReverseStringBuilder(stackalloc char[StackAllocBufferSize]);
        try
        {
            FormatIndexArgument(indexExpression, ref builder);
            var result = builder.ToString();
            return result;
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static void FormatCapturedValue(MemberExpression memberExpression, ConstantExpression constantExpression, ref ReverseStringBuilder builder)
    {
        var member = memberExpression.Member;
        if (!s_capturedValueFormatterCache.TryGetValue(member, out var format))
        {
            format = CreateCapturedValueFormatter(memberExpression);
            s_capturedValueFormatterCache[member] = format;
        }

        format(constantExpression.Value!, ref builder);
    }

    private static CapturedValueFormatter CreateCapturedValueFormatter(MemberExpression memberExpression)
    {
        var memberType = memberExpression.Type;

        if (memberType == typeof(int))
        {
            var func = CompileMemberEvaluator<int>(memberExpression);
            return (object closure, ref ReverseStringBuilder builder) => builder.InsertFront(func.Invoke(closure));
        }
        else if (memberType == typeof(string))
        {
            var func = CompileMemberEvaluator<string>(memberExpression);
            return (object closure, ref ReverseStringBuilder builder) => builder.InsertFront(func.Invoke(closure));
        }
        else if (typeof(ISpanFormattable).IsAssignableFrom(memberType))
        {
            var func = CompileMemberEvaluator<ISpanFormattable>(memberExpression);
            return (object closure, ref ReverseStringBuilder builder) => builder.InsertFront(func.Invoke(closure));
        }
        else if (typeof(IFormattable).IsAssignableFrom(memberType))
        {
            var func = CompileMemberEvaluator<IFormattable>(memberExpression);
            return (object closure, ref ReverseStringBuilder builder) => builder.InsertFront(func.Invoke(closure));
        }
        else
        {
            throw new InvalidOperationException($"Cannot format an index argument of type '{memberType}'.");
        }

        static Func<object, TResult> CompileMemberEvaluator<TResult>(MemberExpression memberExpression)
        {
            var parameterExpression = Expression.Parameter(typeof(object));
            var convertExpression = Expression.Convert(parameterExpression, memberExpression.Member.DeclaringType!);
            var replacedMemberExpression = memberExpression.Update(convertExpression);
            var replacedExpression = Expression.Lambda<Func<object, TResult>>(replacedMemberExpression, parameterExpression);
            return replacedExpression.Compile();
        }
    }

    private static void FormatConstantValue(ConstantExpression constantExpression, ref ReverseStringBuilder builder)
    {
        switch (constantExpression.Value)
        {
            case string s:
                builder.InsertFront(s);
                break;
            case ISpanFormattable spanFormattable:
                // This is better than the formattable case because we don't allocate an extra string.
                builder.InsertFront(spanFormattable);
                break;
            case IFormattable formattable:
                builder.InsertFront(formattable);
                break;
            case null:
                builder.InsertFront("null");
                break;
            case var x:
                throw new InvalidOperationException($"Unable to format constant values of type '{x.GetType()}'.");
        }
    }

    private readonly struct MethodInfoData
    {
        public bool IsSingleArgumentIndexer { get; }

        public MethodInfoData(bool isSingleArgumentIndexer)
        {
            IsSingleArgumentIndexer = isSingleArgumentIndexer;
        }
    }
}

public ref struct ReverseStringBuilder
{
    public const int MinimumRentedArraySize = 1024;

    private static readonly ArrayPool<char> s_arrayPool = ArrayPool<char>.Shared;

    private int _nextEndIndex;
    private Span<char> _currentBuffer;
    private SequenceSegment? _fallbackSequenceSegment;

    // For testing.
    internal readonly int SequenceSegmentCount => _fallbackSequenceSegment?.Count() ?? 0;

    public ReverseStringBuilder(int conservativeEstimatedStringLength)
    {
        var array = s_arrayPool.Rent(conservativeEstimatedStringLength);
        _fallbackSequenceSegment = new(array);
        _currentBuffer = array;
        _nextEndIndex = _currentBuffer.Length;
    }

    public ReverseStringBuilder(Span<char> initialBuffer)
    {
        _currentBuffer = initialBuffer;
        _nextEndIndex = _currentBuffer.Length;
    }

    public readonly bool Empty => _nextEndIndex == _currentBuffer.Length;

    public void InsertFront(scoped ReadOnlySpan<char> span)
    {
        var startIndex = _nextEndIndex - span.Length;
        if (startIndex >= 0)
        {
            // The common case. There is enough space in the current buffer to copy the given span.
            // No additional work needs to be done here after the copy.
            span.CopyTo(_currentBuffer[startIndex..]);
            _nextEndIndex = startIndex;
            return;
        }

        // There wasn't enough space in the current buffer.
        // What we do next depends on whether we're writing to the provided "initial" buffer or a rented one.

        if (_fallbackSequenceSegment is null)
        {
            // We've been writing to a stack-allocated buffer, but there is no more room on the stack.
            // We rent new memory with a length sufficiently larger than the initial buffer
            // and copy the contents over.
            var remainingLength = -startIndex;
            var sizeToRent = _currentBuffer.Length + Math.Max(MinimumRentedArraySize, remainingLength * 2);
            var newBuffer = s_arrayPool.Rent(sizeToRent);
            _fallbackSequenceSegment = new(newBuffer);

            var newEndIndex = newBuffer.Length - _currentBuffer.Length + _nextEndIndex;
            _currentBuffer[_nextEndIndex..].CopyTo(newBuffer.AsSpan(newEndIndex));
            newEndIndex -= span.Length;
            span.CopyTo(newBuffer.AsSpan(newEndIndex));

            _currentBuffer = newBuffer;
            _nextEndIndex = newEndIndex;
        }
        else
        {
            // We can't fit the whole string in the current heap-allocated buffer.
            // Copy as much as we can to the current buffer, rent a new buffer, and
            // continue copying the remaining contents.
            var remainingLength = -startIndex;
            span[remainingLength..].CopyTo(_currentBuffer);
            span = span[..remainingLength];

            var sizeToRent = Math.Max(MinimumRentedArraySize, remainingLength * 2);
            var newBuffer = s_arrayPool.Rent(sizeToRent);
            _fallbackSequenceSegment = new(newBuffer, _fallbackSequenceSegment);
            _currentBuffer = newBuffer;

            startIndex = _currentBuffer.Length - remainingLength;
            span.CopyTo(_currentBuffer[startIndex..]);
            _nextEndIndex = startIndex;
        }
    }

    public void InsertFront<T>(T value) where T : ISpanFormattable
    {
        // This is large enough for any integer value (10 digits plus the possible sign).
        // We won't try to optimize for anything larger.
        Span<char> result = stackalloc char[11];

        if (value.TryFormat(result, out var charsWritten, format: default, CultureInfo.InvariantCulture))
        {
            InsertFront(result[..charsWritten]);
        }
        else
        {
            InsertFront((IFormattable)value);
        }
    }

    public void InsertFront(IFormattable formattable)
        => InsertFront(formattable.ToString(null, CultureInfo.InvariantCulture));

    public override readonly string ToString()
        => _fallbackSequenceSegment is null
            ? new(_currentBuffer[_nextEndIndex..])
            : _fallbackSequenceSegment.ToString(_nextEndIndex);

    public readonly void Dispose()
    {
        _fallbackSequenceSegment?.Dispose();
    }

    private sealed class SequenceSegment : ReadOnlySequenceSegment<char>, IDisposable
    {
        private readonly char[] _array;

        public SequenceSegment(char[] array, SequenceSegment? next = null)
        {
            _array = array;
            Memory = array;
            Next = next;
        }

        // For testing.
        internal int Count()
        {
            var count = 0;
            for (var current = this; current is not null; current = current.Next as SequenceSegment)
            {
                count++;
            }
            return count;
        }

        public string ToString(int startIndex)
        {
            RunningIndex = 0;

            var tail = this;
            while (tail.Next is SequenceSegment next)
            {
                next.RunningIndex = tail.RunningIndex + tail.Memory.Length;
                tail = next;
            }

            var sequence = new ReadOnlySequence<char>(this, startIndex, tail, tail.Memory.Length);
            return sequence.ToString();
        }

        public void Dispose()
        {
            for (var current = this; current is not null; current = current.Next as SequenceSegment)
            {
                s_arrayPool.Return(current._array);
            }
        }
    }
}