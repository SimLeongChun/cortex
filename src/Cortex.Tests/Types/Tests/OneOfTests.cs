using Cortex.Types;

namespace Cortex.Tests.Types.Tests
{
    public class OneOfTests
    {
        #region OneOf<T1, T2> Tests

        [Fact]
        public void OneOf2_ImplicitConversion_FromT1_SetsCorrectTypeIndex()
        {
            OneOf<int, string> value = 42;

            Assert.Equal(0, value.TypeIndex);
            Assert.Equal(42, value.Value);
        }

        [Fact]
        public void OneOf2_ImplicitConversion_FromT2_SetsCorrectTypeIndex()
        {
            OneOf<int, string> value = "hello";

            Assert.Equal(1, value.TypeIndex);
            Assert.Equal("hello", value.Value);
        }

        [Fact]
        public void OneOf2_Is_ReturnsTrue_WhenTypeMatches()
        {
            OneOf<int, string> value = 42;

            Assert.True(value.Is<int>());
            Assert.False(value.Is<string>());
        }

        [Fact]
        public void OneOf2_As_ReturnsValue_WhenTypeMatches()
        {
            OneOf<int, string> value = 42;

            Assert.Equal(42, value.As<int>());
        }

        [Fact]
        public void OneOf2_As_ThrowsInvalidCastException_WhenTypeMismatch()
        {
            OneOf<int, string> value = 42;

            Assert.Throws<InvalidCastException>(() => value.As<string>());
        }

        [Fact]
        public void OneOf2_TryGet_ReturnsTrue_WhenTypeMatches()
        {
            OneOf<int, string> value = 42;

            Assert.True(value.TryGet(out int result));
            Assert.Equal(42, result);
        }

        [Fact]
        public void OneOf2_TryGet_ReturnsFalse_WhenTypeMismatch()
        {
            OneOf<int, string> value = 42;

            Assert.False(value.TryGet(out string? result));
        }

        [Fact]
        public void OneOf2_Match_ExecutesCorrectHandler()
        {
            OneOf<int, string> intValue = 42;
            OneOf<int, string> stringValue = "hello";

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
        public void OneOf2_Switch_ExecutesCorrectAction()
        {
            OneOf<int, string> value = 42;
            int? capturedInt = null;
            string? capturedString = null;

            value.Switch(
                i => capturedInt = i,
                s => capturedString = s);

            Assert.Equal(42, capturedInt);
            Assert.Null(capturedString);
        }

        [Fact]
        public void OneOf2_Equals_ReturnsTrue_ForSameValues()
        {
            OneOf<int, string> value1 = 42;
            OneOf<int, string> value2 = 42;

            Assert.Equal(value1, value2);
            Assert.True(value1 == value2);
            Assert.False(value1 != value2);
        }

        [Fact]
        public void OneOf2_Equals_ReturnsFalse_ForDifferentValues()
        {
            OneOf<int, string> value1 = 42;
            OneOf<int, string> value2 = "hello";

            Assert.NotEqual(value1, value2);
        }

        [Fact]
        public void OneOf2_ToString_ReturnsValueString()
        {
            OneOf<int, string> value = 42;

            Assert.Equal("42", value.ToString());
        }

        #endregion

        #region OneOf<T1, T2, T3> Tests

        [Fact]
        public void OneOf3_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            OneOf<int, string, double> intVal = 42;
            OneOf<int, string, double> strVal = "hello";
            OneOf<int, string, double> dblVal = 3.14;

            Assert.Equal(0, intVal.TypeIndex);
            Assert.Equal(1, strVal.TypeIndex);
            Assert.Equal(2, dblVal.TypeIndex);
        }

        [Fact]
        public void OneOf3_Match_ExecutesCorrectHandler()
        {
            OneOf<int, string, double> value = 3.14;

            var result = value.Match(
                i => "int",
                s => "string",
                d => "double");

            Assert.Equal("double", result);
        }

        [Fact]
        public void OneOf3_Switch_ExecutesCorrectAction()
        {
            OneOf<int, string, double> value = "hello";
            string? captured = null;

            value.Switch(
                i => { },
                s => captured = s,
                d => { });

            Assert.Equal("hello", captured);
        }

        #endregion

        #region OneOf<T1, T2, T3, T4> Tests

        [Fact]
        public void OneOf4_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            OneOf<int, string, double, bool> val1 = 42;
            OneOf<int, string, double, bool> val2 = "hello";
            OneOf<int, string, double, bool> val3 = 3.14;
            OneOf<int, string, double, bool> val4 = true;

            Assert.Equal(0, val1.TypeIndex);
            Assert.Equal(1, val2.TypeIndex);
            Assert.Equal(2, val3.TypeIndex);
            Assert.Equal(3, val4.TypeIndex);
        }

        [Fact]
        public void OneOf4_Match_ExecutesCorrectHandler()
        {
            OneOf<int, string, double, bool> value = true;

            var result = value.Match(
                i => "int",
                s => "string",
                d => "double",
                b => "bool");

            Assert.Equal("bool", result);
        }

        #endregion

        #region OneOf<T1, T2, T3, T4, T5> Tests

        [Fact]
        public void OneOf5_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            OneOf<int, string, double, bool, char> val1 = 42;
            OneOf<int, string, double, bool, char> val2 = "hello";
            OneOf<int, string, double, bool, char> val3 = 3.14;
            OneOf<int, string, double, bool, char> val4 = true;
            OneOf<int, string, double, bool, char> val5 = 'x';

            Assert.Equal(0, val1.TypeIndex);
            Assert.Equal(1, val2.TypeIndex);
            Assert.Equal(2, val3.TypeIndex);
            Assert.Equal(3, val4.TypeIndex);
            Assert.Equal(4, val5.TypeIndex);
        }

        [Fact]
        public void OneOf5_Match_ExecutesCorrectHandler()
        {
            OneOf<int, string, double, bool, char> value = 'x';

            var result = value.Match(
                i => "int",
                s => "string",
                d => "double",
                b => "bool",
                c => "char");

            Assert.Equal("char", result);
        }

        [Fact]
        public void OneOf5_Switch_ExecutesCorrectAction()
        {
            OneOf<int, string, double, bool, char> value = 'x';
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
        public void OneOf5_TryGet_WorksCorrectly()
        {
            OneOf<int, string, double, bool, char> value = 3.14;

            Assert.True(value.TryGet(out double d));
            Assert.Equal(3.14, d);
            Assert.False(value.TryGet(out int _));
        }

        [Fact]
        public void OneOf5_Equals_WorksCorrectly()
        {
            OneOf<int, string, double, bool, char> val1 = 'x';
            OneOf<int, string, double, bool, char> val2 = 'x';
            OneOf<int, string, double, bool, char> val3 = 'y';

            Assert.Equal(val1, val2);
            Assert.NotEqual(val1, val3);
        }

        #endregion

        #region OneOf<T1, T2, T3, T4, T5, T6> Tests

        [Fact]
        public void OneOf6_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            OneOf<int, string, double, bool, char, long> val1 = 42;
            OneOf<int, string, double, bool, char, long> val2 = "hello";
            OneOf<int, string, double, bool, char, long> val3 = 3.14;
            OneOf<int, string, double, bool, char, long> val4 = true;
            OneOf<int, string, double, bool, char, long> val5 = 'x';
            OneOf<int, string, double, bool, char, long> val6 = 100L;

            Assert.Equal(0, val1.TypeIndex);
            Assert.Equal(1, val2.TypeIndex);
            Assert.Equal(2, val3.TypeIndex);
            Assert.Equal(3, val4.TypeIndex);
            Assert.Equal(4, val5.TypeIndex);
            Assert.Equal(5, val6.TypeIndex);
        }

        [Fact]
        public void OneOf6_Match_ExecutesCorrectHandler()
        {
            OneOf<int, string, double, bool, char, long> value = 100L;

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
        public void OneOf6_Switch_ExecutesCorrectAction()
        {
            OneOf<int, string, double, bool, char, long> value = 100L;
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

        #endregion

        #region OneOf<T1, T2, T3, T4, T5, T6, T7> Tests

        [Fact]
        public void OneOf7_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            OneOf<int, string, double, bool, char, long, float> val1 = 42;
            OneOf<int, string, double, bool, char, long, float> val2 = "hello";
            OneOf<int, string, double, bool, char, long, float> val3 = 3.14;
            OneOf<int, string, double, bool, char, long, float> val4 = true;
            OneOf<int, string, double, bool, char, long, float> val5 = 'x';
            OneOf<int, string, double, bool, char, long, float> val6 = 100L;
            OneOf<int, string, double, bool, char, long, float> val7 = 1.5f;

            Assert.Equal(0, val1.TypeIndex);
            Assert.Equal(1, val2.TypeIndex);
            Assert.Equal(2, val3.TypeIndex);
            Assert.Equal(3, val4.TypeIndex);
            Assert.Equal(4, val5.TypeIndex);
            Assert.Equal(5, val6.TypeIndex);
            Assert.Equal(6, val7.TypeIndex);
        }

        [Fact]
        public void OneOf7_Match_ExecutesCorrectHandler()
        {
            OneOf<int, string, double, bool, char, long, float> value = 1.5f;

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
        public void OneOf7_Switch_ExecutesCorrectAction()
        {
            OneOf<int, string, double, bool, char, long, float> value = 1.5f;
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
        public void OneOf7_Equality_WorksCorrectly()
        {
            OneOf<int, string, double, bool, char, long, float> val1 = 1.5f;
            OneOf<int, string, double, bool, char, long, float> val2 = 1.5f;

            Assert.True(val1 == val2);
            Assert.False(val1 != val2);
            Assert.Equal(val1.GetHashCode(), val2.GetHashCode());
        }

        #endregion

        #region OneOf<T1, T2, T3, T4, T5, T6, T7, T8> Tests

        [Fact]
        public void OneOf8_ImplicitConversion_FromEachType_SetsCorrectTypeIndex()
        {
            OneOf<int, string, double, bool, char, long, float, decimal> val1 = 42;
            OneOf<int, string, double, bool, char, long, float, decimal> val2 = "hello";
            OneOf<int, string, double, bool, char, long, float, decimal> val3 = 3.14;
            OneOf<int, string, double, bool, char, long, float, decimal> val4 = true;
            OneOf<int, string, double, bool, char, long, float, decimal> val5 = 'x';
            OneOf<int, string, double, bool, char, long, float, decimal> val6 = 100L;
            OneOf<int, string, double, bool, char, long, float, decimal> val7 = 1.5f;
            OneOf<int, string, double, bool, char, long, float, decimal> val8 = 99.99m;

            Assert.Equal(0, val1.TypeIndex);
            Assert.Equal(1, val2.TypeIndex);
            Assert.Equal(2, val3.TypeIndex);
            Assert.Equal(3, val4.TypeIndex);
            Assert.Equal(4, val5.TypeIndex);
            Assert.Equal(5, val6.TypeIndex);
            Assert.Equal(6, val7.TypeIndex);
            Assert.Equal(7, val8.TypeIndex);
        }

        [Fact]
        public void OneOf8_Match_ExecutesCorrectHandler()
        {
            OneOf<int, string, double, bool, char, long, float, decimal> value = 99.99m;

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
        public void OneOf8_Switch_ExecutesCorrectAction()
        {
            OneOf<int, string, double, bool, char, long, float, decimal> value = 99.99m;
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
        public void OneOf8_Is_WorksForAllTypes()
        {
            OneOf<int, string, double, bool, char, long, float, decimal> value = 99.99m;

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
        public void OneOf8_As_WorksCorrectly()
        {
            OneOf<int, string, double, bool, char, long, float, decimal> value = 99.99m;

            Assert.Equal(99.99m, value.As<decimal>());
            Assert.Throws<InvalidCastException>(() => value.As<int>());
        }

        [Fact]
        public void OneOf8_TryGet_WorksCorrectly()
        {
            OneOf<int, string, double, bool, char, long, float, decimal> value = 99.99m;

            Assert.True(value.TryGet(out decimal d));
            Assert.Equal(99.99m, d);
            Assert.False(value.TryGet(out int _));
        }

        [Fact]
        public void OneOf8_Equality_WorksCorrectly()
        {
            OneOf<int, string, double, bool, char, long, float, decimal> val1 = 99.99m;
            OneOf<int, string, double, bool, char, long, float, decimal> val2 = 99.99m;
            OneOf<int, string, double, bool, char, long, float, decimal> val3 = 100m;

            Assert.True(val1 == val2);
            Assert.False(val1 != val2);
            Assert.True(val1 != val3);
            Assert.Equal(val1.GetHashCode(), val2.GetHashCode());
        }

        [Fact]
        public void OneOf8_ToString_ReturnsValueString()
        {
            OneOf<int, string, double, bool, char, long, float, decimal> value = "hello";

            Assert.Equal("hello", value.ToString());
        }

        #endregion

        #region IOneOf Interface Tests

        [Fact]
        public void AllOneOfTypes_ImplementIOneOf()
        {
            IOneOf oneOf2 = (OneOf<int, string>)42;
            IOneOf oneOf3 = (OneOf<int, string, double>)42;
            IOneOf oneOf4 = (OneOf<int, string, double, bool>)42;
            IOneOf oneOf5 = (OneOf<int, string, double, bool, char>)42;
            IOneOf oneOf6 = (OneOf<int, string, double, bool, char, long>)42;
            IOneOf oneOf7 = (OneOf<int, string, double, bool, char, long, float>)42;
            IOneOf oneOf8 = (OneOf<int, string, double, bool, char, long, float, decimal>)42;

            Assert.Equal(42, oneOf2.Value);
            Assert.Equal(0, oneOf2.TypeIndex);

            Assert.Equal(42, oneOf3.Value);
            Assert.Equal(42, oneOf4.Value);
            Assert.Equal(42, oneOf5.Value);
            Assert.Equal(42, oneOf6.Value);
            Assert.Equal(42, oneOf7.Value);
            Assert.Equal(42, oneOf8.Value);
        }

        #endregion

        #region Inheritance Tests

        [Fact]
        public void OneOf_Is_WorksWithDerivedTypes()
        {
            OneOf<Exception, string> value = new ArgumentException("test");

            Assert.True(value.Is<Exception>());
            Assert.True(value.Is<ArgumentException>());
            Assert.False(value.Is<InvalidOperationException>());
        }

        [Fact]
        public void OneOf_As_WorksWithDerivedTypes()
        {
            OneOf<Exception, string> value = new ArgumentException("test");

            Assert.IsType<ArgumentException>(value.As<Exception>());
            Assert.IsType<ArgumentException>(value.As<ArgumentException>());
        }

        #endregion
    }
}
