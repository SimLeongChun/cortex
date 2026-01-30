using Cortex.Types;

namespace Cortex.Tests.Types.Tests
{
    public class AnyOfTests
    {
        #region AnyOf<T1, T2> Tests

        [Fact]
        public void AnyOf2_ImplicitConversion_FromT1_SetsCorrectTypeIndex()
        {
            AnyOf<int, string> value = 42;

            Assert.Contains(0, value.TypeIndices);
            Assert.Equal(42, value.Value);
        }

        [Fact]
        public void AnyOf2_ImplicitConversion_FromT2_SetsCorrectTypeIndex()
        {
            AnyOf<int, string> value = "hello";

            Assert.Contains(1, value.TypeIndices);
            Assert.Equal("hello", value.Value);
        }

        [Fact]
        public void AnyOf2_Is_ReturnsTrue_WhenTypeMatches()
        {
            AnyOf<int, string> value = 42;

            Assert.True(value.Is<int>());
            Assert.False(value.Is<string>());
        }

        [Fact]
        public void AnyOf2_As_ReturnsValue_WhenTypeMatches()
        {
            AnyOf<int, string> value = 42;

            Assert.Equal(42, value.As<int>());
        }

        [Fact]
        public void AnyOf2_As_ThrowsInvalidCastException_WhenTypeMismatch()
        {
            AnyOf<int, string> value = 42;

            Assert.Throws<InvalidCastException>(() => value.As<string>());
        }

        [Fact]
        public void AnyOf2_TryGet_ReturnsTrue_WhenTypeMatches()
        {
            AnyOf<int, string> value = 42;

            Assert.True(value.TryGet(out int result));
            Assert.Equal(42, result);
        }

        [Fact]
        public void AnyOf2_TryGet_ReturnsFalse_WhenTypeMismatch()
        {
            AnyOf<int, string> value = 42;

            Assert.False(value.TryGet(out string? result));
        }

        [Fact]
        public void AnyOf2_Match_ExecutesCorrectHandler()
        {
            AnyOf<int, string> intValue = 42;
            AnyOf<int, string> stringValue = "hello";

            var intResult = intValue.Match(
                i => $"int: {i}",
                s => $"string: {s}");

            var stringResult = stringValue.Match(
                i => $"int: {i}",
                s => $"string: {s}");

            Assert.Equal("int: 42", intResult);
            Assert.Equal("string: hello", stringResult);
        }

        [Fact]
        public void AnyOf2_Switch_ExecutesCorrectAction()
        {
            AnyOf<int, string> value = 42;
            int? capturedInt = null;
            string? capturedString = null;

            value.Switch(
                i => capturedInt = i,
                s => capturedString = s);

            Assert.Equal(42, capturedInt);
            Assert.Null(capturedString);
        }

        [Fact]
        public void AnyOf2_GetMatchingTypes_ReturnsMatchingTypes()
        {
            AnyOf<int, string> value = 42;

            var matchingTypes = value.GetMatchingTypes().ToList();

            Assert.Single(matchingTypes);
            Assert.Contains(typeof(int), matchingTypes);
        }

        [Fact]
        public void AnyOf2_Equals_ReturnsTrue_ForSameValues()
        {
            AnyOf<int, string> value1 = 42;
            AnyOf<int, string> value2 = 42;

            Assert.Equal(value1, value2);
            Assert.True(value1 == value2);
            Assert.False(value1 != value2);
        }

        [Fact]
        public void AnyOf2_ToString_ReturnsValueString()
        {
            AnyOf<int, string> value = 42;

            Assert.Equal("42", value.ToString());
        }

        #endregion

        #region AnyOf<T1, T2, T3> Tests

        [Fact]
        public void AnyOf3_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            AnyOf<int, string, double> intVal = 42;
            AnyOf<int, string, double> strVal = "hello";
            AnyOf<int, string, double> dblVal = 3.14;

            Assert.Contains(0, intVal.TypeIndices);
            Assert.Contains(1, strVal.TypeIndices);
            Assert.Contains(2, dblVal.TypeIndices);
        }

        [Fact]
        public void AnyOf3_Match_ExecutesCorrectHandler()
        {
            AnyOf<int, string, double> value = 3.14;

            var result = value.Match(
                i => "int",
                s => "string",
                d => "double");

            Assert.Equal("double", result);
        }

        [Fact]
        public void AnyOf3_Switch_ExecutesCorrectAction()
        {
            AnyOf<int, string, double> value = "hello";
            string? captured = null;

            value.Switch(
                i => { },
                s => captured = s,
                d => { });

            Assert.Equal("hello", captured);
        }

        [Fact]
        public void AnyOf3_GetMatchingTypes_ReturnsMatchingTypes()
        {
            AnyOf<int, string, double> value = 3.14;

            var matchingTypes = value.GetMatchingTypes().ToList();

            Assert.Single(matchingTypes);
            Assert.Contains(typeof(double), matchingTypes);
        }

        #endregion

        #region AnyOf<T1, T2, T3, T4> Tests

        [Fact]
        public void AnyOf4_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            AnyOf<int, string, double, bool> val1 = 42;
            AnyOf<int, string, double, bool> val2 = "hello";
            AnyOf<int, string, double, bool> val3 = 3.14;
            AnyOf<int, string, double, bool> val4 = true;

            Assert.Contains(0, val1.TypeIndices);
            Assert.Contains(1, val2.TypeIndices);
            Assert.Contains(2, val3.TypeIndices);
            Assert.Contains(3, val4.TypeIndices);
        }

        [Fact]
        public void AnyOf4_Match_ExecutesCorrectHandler()
        {
            AnyOf<int, string, double, bool> value = true;

            var result = value.Match(
                i => "int",
                s => "string",
                d => "double",
                b => "bool");

            Assert.Equal("bool", result);
        }

        [Fact]
        public void AnyOf4_GetMatchingTypes_ReturnsMatchingTypes()
        {
            AnyOf<int, string, double, bool> value = true;

            var matchingTypes = value.GetMatchingTypes().ToList();

            Assert.Single(matchingTypes);
            Assert.Contains(typeof(bool), matchingTypes);
        }

        #endregion

        #region AnyOf<T1, T2, T3, T4, T5> Tests

        [Fact]
        public void AnyOf5_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            AnyOf<int, string, double, bool, char> val1 = 42;
            AnyOf<int, string, double, bool, char> val2 = "hello";
            AnyOf<int, string, double, bool, char> val3 = 3.14;
            AnyOf<int, string, double, bool, char> val4 = true;
            AnyOf<int, string, double, bool, char> val5 = 'x';

            Assert.Contains(0, val1.TypeIndices);
            Assert.Contains(1, val2.TypeIndices);
            Assert.Contains(2, val3.TypeIndices);
            Assert.Contains(3, val4.TypeIndices);
            Assert.Contains(4, val5.TypeIndices);
        }

        [Fact]
        public void AnyOf5_Match_ExecutesCorrectHandler()
        {
            AnyOf<int, string, double, bool, char> value = 'x';

            var result = value.Match(
                i => "int",
                s => "string",
                d => "double",
                b => "bool",
                c => "char");

            Assert.Equal("char", result);
        }

        [Fact]
        public void AnyOf5_Switch_ExecutesCorrectAction()
        {
            AnyOf<int, string, double, bool, char> value = 'x';
            char? captured = null;

            value.Switch(
                i => { },
                s => { },
                d => { },
                b => { },
                c => captured = c);

            Assert.Equal('x', captured);
        }

        [Fact]
        public void AnyOf5_TryGet_WorksCorrectly()
        {
            AnyOf<int, string, double, bool, char> value = 3.14;

            Assert.True(value.TryGet(out double d));
            Assert.Equal(3.14, d);
            Assert.False(value.TryGet(out int _));
        }

        [Fact]
        public void AnyOf5_GetMatchingTypes_ReturnsMatchingTypes()
        {
            AnyOf<int, string, double, bool, char> value = 'x';

            var matchingTypes = value.GetMatchingTypes().ToList();

            Assert.Single(matchingTypes);
            Assert.Contains(typeof(char), matchingTypes);
        }

        [Fact]
        public void AnyOf5_Equals_WorksCorrectly()
        {
            AnyOf<int, string, double, bool, char> val1 = 'x';
            AnyOf<int, string, double, bool, char> val2 = 'x';
            AnyOf<int, string, double, bool, char> val3 = 'y';

            Assert.Equal(val1, val2);
            Assert.NotEqual(val1, val3);
        }

        #endregion

        #region AnyOf<T1, T2, T3, T4, T5, T6> Tests

        [Fact]
        public void AnyOf6_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            AnyOf<int, string, double, bool, char, long> val1 = 42;
            AnyOf<int, string, double, bool, char, long> val2 = "hello";
            AnyOf<int, string, double, bool, char, long> val3 = 3.14;
            AnyOf<int, string, double, bool, char, long> val4 = true;
            AnyOf<int, string, double, bool, char, long> val5 = 'x';
            AnyOf<int, string, double, bool, char, long> val6 = 100L;

            Assert.Contains(0, val1.TypeIndices);
            Assert.Contains(1, val2.TypeIndices);
            Assert.Contains(2, val3.TypeIndices);
            Assert.Contains(3, val4.TypeIndices);
            Assert.Contains(4, val5.TypeIndices);
            Assert.Contains(5, val6.TypeIndices);
        }

        [Fact]
        public void AnyOf6_Match_ExecutesCorrectHandler()
        {
            AnyOf<int, string, double, bool, char, long> value = 100L;

            var result = value.Match(
                i => "int",
                s => "string",
                d => "double",
                b => "bool",
                c => "char",
                l => "long");

            Assert.Equal("long", result);
        }

        [Fact]
        public void AnyOf6_Switch_ExecutesCorrectAction()
        {
            AnyOf<int, string, double, bool, char, long> value = 100L;
            long? captured = null;

            value.Switch(
                i => { },
                s => { },
                d => { },
                b => { },
                c => { },
                l => captured = l);

            Assert.Equal(100L, captured);
        }

        [Fact]
        public void AnyOf6_GetMatchingTypes_ReturnsMatchingTypes()
        {
            AnyOf<int, string, double, bool, char, long> value = 100L;

            var matchingTypes = value.GetMatchingTypes().ToList();

            Assert.Single(matchingTypes);
            Assert.Contains(typeof(long), matchingTypes);
        }

        #endregion

        #region AnyOf<T1, T2, T3, T4, T5, T6, T7> Tests

        [Fact]
        public void AnyOf7_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            AnyOf<int, string, double, bool, char, long, float> val1 = 42;
            AnyOf<int, string, double, bool, char, long, float> val2 = "hello";
            AnyOf<int, string, double, bool, char, long, float> val3 = 3.14;
            AnyOf<int, string, double, bool, char, long, float> val4 = true;
            AnyOf<int, string, double, bool, char, long, float> val5 = 'x';
            AnyOf<int, string, double, bool, char, long, float> val6 = 100L;
            AnyOf<int, string, double, bool, char, long, float> val7 = 1.5f;

            Assert.Contains(0, val1.TypeIndices);
            Assert.Contains(1, val2.TypeIndices);
            Assert.Contains(2, val3.TypeIndices);
            Assert.Contains(3, val4.TypeIndices);
            Assert.Contains(4, val5.TypeIndices);
            Assert.Contains(5, val6.TypeIndices);
            Assert.Contains(6, val7.TypeIndices);
        }

        [Fact]
        public void AnyOf7_Match_ExecutesCorrectHandler()
        {
            AnyOf<int, string, double, bool, char, long, float> value = 1.5f;

            var result = value.Match(
                i => "int",
                s => "string",
                d => "double",
                b => "bool",
                c => "char",
                l => "long",
                f => "float");

            Assert.Equal("float", result);
        }

        [Fact]
        public void AnyOf7_Switch_ExecutesCorrectAction()
        {
            AnyOf<int, string, double, bool, char, long, float> value = 1.5f;
            float? captured = null;

            value.Switch(
                i => { },
                s => { },
                d => { },
                b => { },
                c => { },
                l => { },
                f => captured = f);

            Assert.Equal(1.5f, captured);
        }

        [Fact]
        public void AnyOf7_GetMatchingTypes_ReturnsMatchingTypes()
        {
            AnyOf<int, string, double, bool, char, long, float> value = 1.5f;

            var matchingTypes = value.GetMatchingTypes().ToList();

            Assert.Single(matchingTypes);
            Assert.Contains(typeof(float), matchingTypes);
        }

        [Fact]
        public void AnyOf7_Equality_WorksCorrectly()
        {
            AnyOf<int, string, double, bool, char, long, float> val1 = 1.5f;
            AnyOf<int, string, double, bool, char, long, float> val2 = 1.5f;

            Assert.True(val1 == val2);
            Assert.False(val1 != val2);
        }

        #endregion

        #region AnyOf<T1, T2, T3, T4, T5, T6, T7, T8> Tests

        [Fact]
        public void AnyOf8_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            AnyOf<int, string, double, bool, char, long, float, decimal> val1 = 42;
            AnyOf<int, string, double, bool, char, long, float, decimal> val2 = "hello";
            AnyOf<int, string, double, bool, char, long, float, decimal> val3 = 3.14;
            AnyOf<int, string, double, bool, char, long, float, decimal> val4 = true;
            AnyOf<int, string, double, bool, char, long, float, decimal> val5 = 'x';
            AnyOf<int, string, double, bool, char, long, float, decimal> val6 = 100L;
            AnyOf<int, string, double, bool, char, long, float, decimal> val7 = 1.5f;
            AnyOf<int, string, double, bool, char, long, float, decimal> val8 = 99.99m;

            Assert.Contains(0, val1.TypeIndices);
            Assert.Contains(1, val2.TypeIndices);
            Assert.Contains(2, val3.TypeIndices);
            Assert.Contains(3, val4.TypeIndices);
            Assert.Contains(4, val5.TypeIndices);
            Assert.Contains(5, val6.TypeIndices);
            Assert.Contains(6, val7.TypeIndices);
            Assert.Contains(7, val8.TypeIndices);
        }

        [Fact]
        public void AnyOf8_Match_ExecutesCorrectHandler()
        {
            AnyOf<int, string, double, bool, char, long, float, decimal> value = 99.99m;

            var result = value.Match(
                i => "int",
                s => "string",
                d => "double",
                b => "bool",
                c => "char",
                l => "long",
                f => "float",
                m => "decimal");

            Assert.Equal("decimal", result);
        }

        [Fact]
        public void AnyOf8_Switch_ExecutesCorrectAction()
        {
            AnyOf<int, string, double, bool, char, long, float, decimal> value = 99.99m;
            decimal? captured = null;

            value.Switch(
                i => { },
                s => { },
                d => { },
                b => { },
                c => { },
                l => { },
                f => { },
                m => captured = m);

            Assert.Equal(99.99m, captured);
        }

        [Fact]
        public void AnyOf8_Is_WorksForAllTypes()
        {
            AnyOf<int, string, double, bool, char, long, float, decimal> value = 99.99m;

            Assert.False(value.Is<int>());
            Assert.False(value.Is<string>());
            Assert.False(value.Is<double>());
            Assert.False(value.Is<bool>());
            Assert.False(value.Is<char>());
            Assert.False(value.Is<long>());
            Assert.False(value.Is<float>());
            Assert.True(value.Is<decimal>());
        }

        [Fact]
        public void AnyOf8_As_WorksCorrectly()
        {
            AnyOf<int, string, double, bool, char, long, float, decimal> value = 99.99m;

            Assert.Equal(99.99m, value.As<decimal>());
            Assert.Throws<InvalidCastException>(() => value.As<int>());
        }

        [Fact]
        public void AnyOf8_TryGet_WorksCorrectly()
        {
            AnyOf<int, string, double, bool, char, long, float, decimal> value = 99.99m;

            Assert.True(value.TryGet(out decimal d));
            Assert.Equal(99.99m, d);
            Assert.False(value.TryGet(out int _));
        }

        [Fact]
        public void AnyOf8_GetMatchingTypes_ReturnsMatchingTypes()
        {
            AnyOf<int, string, double, bool, char, long, float, decimal> value = 99.99m;

            var matchingTypes = value.GetMatchingTypes().ToList();

            Assert.Single(matchingTypes);
            Assert.Contains(typeof(decimal), matchingTypes);
        }

        [Fact]
        public void AnyOf8_Equality_WorksCorrectly()
        {
            AnyOf<int, string, double, bool, char, long, float, decimal> val1 = 99.99m;
            AnyOf<int, string, double, bool, char, long, float, decimal> val2 = 99.99m;
            AnyOf<int, string, double, bool, char, long, float, decimal> val3 = 100m;

            Assert.True(val1 == val2);
            Assert.False(val1 != val2);
            Assert.True(val1 != val3);
        }

        [Fact]
        public void AnyOf8_ToString_ReturnsValueString()
        {
            AnyOf<int, string, double, bool, char, long, float, decimal> value = "hello";

            Assert.Equal("hello", value.ToString());
        }

        #endregion

        #region IAnyOf Interface Tests

        [Fact]
        public void AllAnyOfTypes_ImplementIAnyOf()
        {
            IAnyOf anyOf2 = (AnyOf<int, string>)42;
            IAnyOf anyOf3 = (AnyOf<int, string, double>)42;
            IAnyOf anyOf4 = (AnyOf<int, string, double, bool>)42;
            IAnyOf anyOf5 = (AnyOf<int, string, double, bool, char>)42;
            IAnyOf anyOf6 = (AnyOf<int, string, double, bool, char, long>)42;
            IAnyOf anyOf7 = (AnyOf<int, string, double, bool, char, long, float>)42;
            IAnyOf anyOf8 = (AnyOf<int, string, double, bool, char, long, float, decimal>)42;

            Assert.Equal(42, anyOf2.Value);
            Assert.Contains(0, anyOf2.TypeIndices);

            Assert.Equal(42, anyOf3.Value);
            Assert.Equal(42, anyOf4.Value);
            Assert.Equal(42, anyOf5.Value);
            Assert.Equal(42, anyOf6.Value);
            Assert.Equal(42, anyOf7.Value);
            Assert.Equal(42, anyOf8.Value);
        }

        #endregion

        #region Inheritance/Polymorphism Tests

        [Fact]
        public void AnyOf_GetMatchingTypes_IncludesBaseTypes()
        {
            // ArgumentException derives from Exception
            AnyOf<Exception, string> value = new ArgumentException("test");

            var matchingTypes = value.GetMatchingTypes().ToList();

            // Both Exception (base) and the actual type should match
            Assert.Contains(typeof(Exception), matchingTypes);
        }

        [Fact]
        public void AnyOf_Is_WorksWithDerivedTypes()
        {
            AnyOf<Exception, string> value = new ArgumentException("test");

            Assert.True(value.Is<Exception>());
            Assert.True(value.Is<ArgumentException>());
            Assert.False(value.Is<InvalidOperationException>());
        }

        [Fact]
        public void AnyOf_As_WorksWithDerivedTypes()
        {
            AnyOf<Exception, string> value = new ArgumentException("test");

            Assert.IsType<ArgumentException>(value.As<Exception>());
            Assert.IsType<ArgumentException>(value.As<ArgumentException>());
        }

        #endregion
    }
}
