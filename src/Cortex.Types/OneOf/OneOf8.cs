using System;
using System.Diagnostics.CodeAnalysis;

namespace Cortex.Types
{
    /// <summary>
    /// Represents a value that can be one of eight specified types
    /// </summary>
    /// <typeparam name="T1">First possible type</typeparam>
    /// <typeparam name="T2">Second possible type</typeparam>
    /// <typeparam name="T3">Third possible type</typeparam>
    /// <typeparam name="T4">Fourth possible type</typeparam>
    /// <typeparam name="T5">Fifth possible type</typeparam>
    /// <typeparam name="T6">Sixth possible type</typeparam>
    /// <typeparam name="T7">Seventh possible type</typeparam>
    /// <typeparam name="T8">Eighth possible type</typeparam>
    public readonly struct OneOf<T1, T2, T3, T4, T5, T6, T7, T8> : IEquatable<OneOf<T1, T2, T3, T4, T5, T6, T7, T8>>, IOneOf
    {
        private readonly object _value;
        private readonly int _typeIndex;

        /// <inheritdoc />
        public object Value => _value;

        /// <inheritdoc />
        public int TypeIndex => _typeIndex;

        private OneOf(object value, int typeIndex) =>
            (_value, _typeIndex) = (value, typeIndex);

        public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T1 value) => new(value, 0);
        public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T2 value) => new(value, 1);
        public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T3 value) => new(value, 2);
        public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T4 value) => new(value, 3);
        public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T5 value) => new(value, 4);
        public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T6 value) => new(value, 5);
        public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T7 value) => new(value, 6);
        public static implicit operator OneOf<T1, T2, T3, T4, T5, T6, T7, T8>(T8 value) => new(value, 7);

        /// <summary>
        /// Checks if the contained value is of or derived from type <typeparamref name="T"/>
        /// </summary>
        public bool Is<T>() => _value is T;

        /// <summary>
        /// Returns the contained value as <typeparamref name="T"/>
        /// </summary>
        /// <exception cref="InvalidCastException">
        /// Thrown when value is not compatible with <typeparamref name="T"/>
        /// </exception>
        public T As<T>() => _value is T val
            ? val
            : throw new InvalidCastException(GetCastErrorMessage(typeof(T)));

        /// <summary>
        /// Attempts to retrieve the value as <typeparamref name="T"/>
        /// </summary>
        public bool TryGet<T>([NotNullWhen(true)] out T result)
        {
            if (_value is T val)
            {
                result = val;
                return true;
            }

            result = default!;
            return false;
        }

        /// <summary>
        /// Type-safe pattern matching with exhaustive case handling
        /// </summary>
        public TResult Match<TResult>(
            Func<T1, TResult> t1Handler,
            Func<T2, TResult> t2Handler,
            Func<T3, TResult> t3Handler,
            Func<T4, TResult> t4Handler,
            Func<T5, TResult> t5Handler,
            Func<T6, TResult> t6Handler,
            Func<T7, TResult> t7Handler,
            Func<T8, TResult> t8Handler) => _typeIndex switch
            {
                0 => t1Handler((T1)_value),
                1 => t2Handler((T2)_value),
                2 => t3Handler((T3)_value),
                3 => t4Handler((T4)_value),
                4 => t5Handler((T5)_value),
                5 => t6Handler((T6)_value),
                6 => t7Handler((T7)_value),
                7 => t8Handler((T8)_value),
                _ => throw new InvalidOperationException("Invalid state")
            };

        /// <summary>
        /// Executes type-specific action with exhaustive case handling
        /// </summary>
        public void Switch(
            Action<T1> t1Action,
            Action<T2> t2Action,
            Action<T3> t3Action,
            Action<T4> t4Action,
            Action<T5> t5Action,
            Action<T6> t6Action,
            Action<T7> t7Action,
            Action<T8> t8Action)
        {
            switch (_typeIndex)
            {
                case 0: t1Action((T1)_value); break;
                case 1: t2Action((T2)_value); break;
                case 2: t3Action((T3)_value); break;
                case 3: t4Action((T4)_value); break;
                case 4: t5Action((T5)_value); break;
                case 5: t6Action((T6)_value); break;
                case 6: t7Action((T7)_value); break;
                case 7: t8Action((T8)_value); break;
                default: throw new InvalidOperationException("Invalid state");
            }
        }

        private string GetCastErrorMessage(Type targetType) =>
            $"Cannot cast stored type {_value?.GetType().Name ?? "null"} to {targetType.Name}";

        public bool Equals(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> other) =>
            _typeIndex == other._typeIndex &&
            Equals(_value, other._value);

        public override bool Equals(object obj) =>
            obj is OneOf<T1, T2, T3, T4, T5, T6, T7, T8> other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(_value, _typeIndex);

        public static bool operator ==(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> left, OneOf<T1, T2, T3, T4, T5, T6, T7, T8> right) =>
            left.Equals(right);

        public static bool operator !=(OneOf<T1, T2, T3, T4, T5, T6, T7, T8> left, OneOf<T1, T2, T3, T4, T5, T6, T7, T8> right) =>
            !left.Equals(right);

        public override string ToString() =>
            _value?.ToString() ?? string.Empty;
    }
}
