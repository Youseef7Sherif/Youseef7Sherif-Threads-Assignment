namespace OrderProcessing.Shared;

public static class Money
{
    public static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}