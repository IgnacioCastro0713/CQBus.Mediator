using CQBus.Mediator.Messages;

namespace Mediator.Tests.Messages;

public class UnitTest
{
    [Fact]
    public async Task ValueTask_ReturnsCompletedTaskWithSingletonInstance()
    {
        // Arrange
        Unit expectedUnit = Unit.Value;

        // Act
        Unit actualUnit = await Unit.ValueTask;

        // Assert
        Assert.True(actualUnit.Equals(expectedUnit));
        Assert.True(Unit.ValueTask.IsCompletedSuccessfully);
    }

    [Fact]
    public void Equals_WithOtherUnit_ReturnsTrue()
    {
        // Arrange
        Unit unit1 = Unit.Value;
        Unit unit2 = Unit.Value;

        // Act & Assert
        Assert.True(unit1.Equals(unit2));
    }

    [Fact]
    public void Equals_WithObjectUnit_ReturnsTrue()
    {
        // Arrange
        Unit unit1 = Unit.Value;
        object unit2AsObject = Unit.Value;

        // Act & Assert
        Assert.True(unit1.Equals(unit2AsObject));
    }

    [Fact]
    public void Equals_WithObjectNonUnit_ReturnsFalse()
    {
        // Arrange
        Unit unit = Unit.Value;
        object nonUnit = "some string";

        // Act & Assert
        Assert.False(unit.Equals(nonUnit));
    }

    [Fact]
    public void GetHashCode_ReturnsZero()
    {
        // Arrange
        Unit unit = Unit.Value;

        // Act
        int hashCode = unit.GetHashCode();

        // Assert
        Assert.Equal(0, hashCode);
    }

    [Fact]
    public void CompareTo_WithOtherUnit_ReturnsZero()
    {
        // Arrange
        Unit unit1 = Unit.Value;
        Unit unit2 = Unit.Value;

        // Act
        int result = unit1.CompareTo(unit2);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void CompareTo_WithObject_ReturnsZero()
    {
        // Arrange
        Unit unit1 = Unit.Value;
        object unit2AsObject = Unit.Value;

        // Act
        int result = ((IComparable)unit1).CompareTo(unit2AsObject);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void OperatorEquals_ReturnsTrue()
    {
        // Arrange
        Unit unit1 = Unit.Value;
        Unit unit2 = Unit.Value;

        // Act & Assert
        Assert.True(unit1 == unit2);
    }

    [Fact]
    public void OperatorNotEquals_ReturnsFalse()
    {
        // Arrange
        Unit unit1 = Unit.Value;
        Unit unit2 = Unit.Value;

        // Act & Assert
        Assert.False(unit1 != unit2);
    }

    [Fact]
    public void ToString_ReturnsParentheses()
    {
        // Arrange
        Unit unit = Unit.Value;

        // Act
        string result = unit.ToString();

        // Assert
        Assert.Equal("()", result);
    }

    [Fact]
    public async Task UnitAsVoidReturnType_SimulatesMediatorCommandCompletion()
    {
        // Arrange
        Func<Task<Unit>> commandHandler = async () =>
        {
            await Task.Delay(1);
            return Unit.Value;
        };

        // Act
        Unit result = await commandHandler();

        // Assert
        Assert.Equal(Unit.Value, result);
        Assert.True(result.Equals(Unit.Value));
    }
}
