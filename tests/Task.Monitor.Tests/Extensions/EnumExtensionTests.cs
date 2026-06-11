using Task.Monitor.Extensions;
using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Tests.EnumExtensions;

public sealed class EnumExtensionTests
{
    public enum TestEnum
    {
        [ProcessControl.ColumnProperty("FirstValue")]
        [ProcessControl.ColumnTitle("First Value")]
        First,

        [ProcessControl.ColumnProperty("SecondValue")]
        [ProcessControl.ColumnTitle("Second Value")]
        Second,

        [ProcessControl.ColumnProperty("ThirdValue")]
        [ProcessControl.ColumnTitle("Third Value")]
        Third
    }

    public enum EnumWithoutAttributes
    {
        NoAttribute
    }

    public enum EnumWithPartialAttributes
    {
        [ProcessControl.ColumnProperty("OnlyProperty")]
        OnlyHasProperty,

        [ProcessControl.ColumnTitle("Only Title")]
        OnlyHasTitle
    }

    public class EnumExtensionsTests
    {
        [Fact]
        public void GetProperty_WithValidAttribute_ReturnsProperty()
        {
            var enumValue = TestEnum.First;
            string result = enumValue.GetProperty();
            Assert.Equal("FirstValue", result);
        }

        [Fact]
        public void GetProperty_WithDifferentEnumValues_ReturnsCorrectProperties()
        {
            Assert.Equal("FirstValue", TestEnum.First.GetProperty());
            Assert.Equal("SecondValue", TestEnum.Second.GetProperty());
            Assert.Equal("ThirdValue", TestEnum.Third.GetProperty());
        }

        [Fact]
        public void GetTitle_WithValidAttribute_ReturnsTitle()
        {
            var enumValue = TestEnum.First;
            string result = enumValue.GetTitle();
            Assert.Equal("First Value", result);
        }

        [Fact]
        public void GetTitle_WithDifferentEnumValues_ReturnsCorrectTitles()
        {
            Assert.Equal("First Value", TestEnum.First.GetTitle());
            Assert.Equal("Second Value", TestEnum.Second.GetTitle());
            Assert.Equal("Third Value", TestEnum.Third.GetTitle());
        }

        [Fact]
        public void GetProperty_WithoutAttribute_ThrowsInvalidOperationException()
        {
            var enumValue = EnumWithoutAttributes.NoAttribute;
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => enumValue.GetProperty());
            
            Assert.Contains("has no Attribute", exception.Message);
        }

        [Fact]
        public void GetTitle_WithoutAttribute_ThrowsInvalidOperationException()
        {
            var enumValue = EnumWithoutAttributes.NoAttribute;
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => enumValue.GetTitle());
            
            Assert.Contains("has no Attribute", exception.Message);
        }

        [Fact]
        public void GetProperty_WithOnlyTitleAttribute_ThrowsInvalidOperationException()
        {
            var enumValue = EnumWithPartialAttributes.OnlyHasTitle;
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => enumValue.GetProperty());
            
            Assert.Contains("has no Attribute", exception.Message);
        }

        [Fact]
        public void GetTitle_WithOnlyPropertyAttribute_ThrowsInvalidOperationException()
        {
            var enumValue = EnumWithPartialAttributes.OnlyHasProperty;
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => enumValue.GetTitle());
            
            Assert.Contains("has no Attribute", exception.Message);
        }

        [Fact]
        public void GetProperty_WithValidPartialAttribute_ReturnsProperty()
        {
            var enumValue = EnumWithPartialAttributes.OnlyHasProperty;
            string result = enumValue.GetProperty();
            
            Assert.Equal("OnlyProperty", result);
        }

        [Fact]
        public void GetTitle_WithValidPartialAttribute_ReturnsTitle()
        {
            var enumValue = EnumWithPartialAttributes.OnlyHasTitle;
            string result = enumValue.GetTitle();
            
            Assert.Equal("Only Title", result);
        }
    }
}