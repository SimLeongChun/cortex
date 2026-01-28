using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Cortex.Types
{
    /// <summary>
    /// Represents a value that can be any of eight specified types
    /// </summary>
    /// <typeparam name="T1">First possible type</typeparam>
    /// <typeparam name="T2">Second possible type</typeparam>
    /// <typeparam name="T3">Third possible type</typeparam>
    /// <typeparam name="T4">Fourth possible type</typeparam>
    /// <typeparam name="T5">Fifth possible type</typeparam>
    /// <typeparam name="T6">Sixth possible type</typeparam>
    /// <typeparam name="T7">Seventh possible type</typeparam>
    /// <typeparam name="T8">Eighth possible type</typeparam>
    public readonly struct AnyOf<T1, T2, T3, T4, T5, T6, T7, T8> : IEquatable<AnyOf<T1, T2, T3, T4, T5, T6, T7, T8>>, IAnyOf
    {
        private readonly object _value;
        private readonly HashSet<int> _typeIndices;

        /// <inheritdoc />
        public object Value => _value;

        /// <inheritdoc />
        public IEnumerable<int> TypeIndices => _typeIndices;

        private AnyOf(object value, HashSet<int> typeIndices) =>
            (_value, _typeIndices) = (value, typeIndices);

        public static implicit operator AnyOf<T1, T2, T3, T4, T5, T6, T7, T8>(T1 value) => new(value, new HashSet<int> { 0 });
        public static implicit operator AnyOf<T1, T2, T3, T4, T5, T6, T7, T8>(T2 value) => new(value, new HashSet<int> { 1 });
        public static implicit operator AnyOf<T1, T2, T3, T4, T5, T6, T7, T8>(T3 value) => new(value, new HashSet<int> { 2 });
        public static implicit operator AnyOf<T1, T2, T3, T4, T5, T6, T7, T8>(T4 value) => new(value, new HashSet<int> { 3 });
        public static implicit operator AnyOf<T1, T2, T3, T4, T5, T6, T7, T8>(T5 value) => new(value, new HashSet<int> { 4 });
        public static implicit operator AnyOf<T1, T2, T3, T4, T5, T6, T7, T8>(T6 value) => new(value, new HashSet<int> { 5 });
        public static implicit operator AnyOf<T1, T2, T3, T4, T5, T6, T7, T8>(T7 value) => new(value, new HashSet<int> { 6 });
        public static implicit operator AnyOf<T1, T2, T3, T4, T5, T6, T7, T8>(T8 value) => new(value, new HashSet<int> { 7 });

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
            Func<T8, TResult> t8Handler)
        {
            if (_typeIndices.Contains(0) && _value is T1 t1) return t1Handler(t1);
            if (_typeIndices.Contains(1) && _value is T2 t2) return t2Handler(t2);
            if (_typeIndices.Contains(2) && _value is T3 t3) return t3Handler(t3);
            if (_typeIndices.Contains(3) && _value is T4 t4) return t4Handler(t4);
            if (_typeIndices.Contains(4) && _value is T5 t5) return t5Handler(t5);
            if (_typeIndices.Contains(5) && _value is T6 t6) return t6Handler(t6);
            if (_typeIndices.Contains(6) && _value is T7 t7) return t7Handler(t7);
            if (_typeIndices.Contains(7) && _value is T8 t8) return t8Handler(t8);
            throw new InvalidOperationException("Invalid state");
        }

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
            if (_typeIndices.Contains(0) && _value is T1 t1) { t1Action(t1); return; }
            if (_typeIndices.Contains(1) && _value is T2 t2) { t2Action(t2); return; }
            if (_typeIndices.Contains(2) && _value is T3 t3) { t3Action(t3); return; }
            if (_typeIndices.Contains(3) && _value is T4 t4) { t4Action(t4); return; }
            if (_typeIndices.Contains(4) && _value is T5 t5) { t5Action(t5); return; }
            if (_typeIndices.Contains(5) && _value is T6 t6) { t6Action(t6); return; }
            if (_typeIndices.Contains(6) && _value is T7 t7) { t7Action(t7); return; }
            if (_typeIndices.Contains(7) && _value is T8 t8) { t8Action(t8); return; }
            throw new InvalidOperationException("Invalid state");
        }

        /// <summary>
        /// Returns all of the type parameters for which the stored value is assignable.
        /// </summary>
        public IEnumerable<Type> GetMatchingTypes()
        {
            if (_value is T1) yield return typeof(T1);
            if (_value is T2) yield return typeof(T2);
            if (_value is T3) yield return typeof(T3);
            if (_value is T4) yield return typeof(T4);
            if (_value is T5) yield return typeof(T5);
            if (_value is T6) yield return typeof(T6);
            if (_value is T7) yield return typeof(T7);
            if (_value is T8) yield return typeof(T8);
        }

        private string GetCastErrorMessage(Type targetType) =>
            $"Cannot cast stored type {_value?.GetType().Name ?? "null"} to {targetType.Name}";

        public bool Equals(AnyOf<T1, T2, T3, T4, T5, T6, T7, T8> other) =>
            _typeIndices.SetEquals(other._typeIndices) &&
            Equals(_value, other._value);

        public override bool Equals(object obj) =>
            obj is AnyOf<T1, T2, T3, T4, T5, T6, T7, T8> other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(_value, _typeIndices);

        public static bool operator ==(AnyOf<T1, T2, T3, T4, T5, T6, T7, T8> left, AnyOf<T1, T2, T3, T4, T5, T6, T7, T8> right) =>
            left.Equals(right);

        public static bool operator !=(AnyOf<T1, T2, T3, T4, T5, T6, T7, T8> left, AnyOf<T1, T2, T3, T4, T5, T6, T7, T8> right) =>
            !left.Equals(right);

        public override string ToString() =>
            _value?.ToString() ?? string.Empty;
    }
}
