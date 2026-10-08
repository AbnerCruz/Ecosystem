namespace MathAuthoring.Core;

public abstract record MathExpression;

public sealed record ConstantExpression(double Value) : MathExpression;

public sealed record VariableReferenceExpression(string VariableId) : MathExpression;

public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Power
}

public sealed record BinaryExpression(BinaryOperator Operator, MathExpression Left, MathExpression Right) : MathExpression;
