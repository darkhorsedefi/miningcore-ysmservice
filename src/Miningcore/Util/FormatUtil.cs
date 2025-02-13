namespace Miningcore.Util;

public static class FormatUtil
{
    public static readonly string[] HashrateUnits = { " H/s", " KH/s", " MH/s", " GH/s", " TH/s", " PH/s" , " EH/s" };
    public static readonly string[] QuantityUnits = { "K", "M", "G", "T", "P", "E" };
    public static readonly string[] CapacityUnits = { " KB", " MB", " GB", " TB", " PB" };

    public static string FormatHashrate(double hashrate)
    {
        var i = 0;

        while (hashrate > 1024 && i < HashrateUnits.Length - 1)
        {
            hashrate /= 1024;
            i++;
        }

        return Math.Round(hashrate, 2).ToString("F2") + HashrateUnits[Math.Min(i, HashrateUnits.Length - 1)];
    }

    public static string FormatCapacity(double capacity)
    {
        var i = -1;

        do
        {
            capacity /= 1024;
            i++;
        } while(capacity > 1024 && i < CapacityUnits.Length - 1);

        i = Math.Min(i, CapacityUnits.Length - 1);
        return (int) Math.Abs(capacity) + CapacityUnits[i];
    }

    public static string FormatQuantity(double value)
    {
        var i = -1;

        do
        {
            value /= 1000;
            i++;
        } while(value > 1000 && i < QuantityUnits.Length - 1);

        i = Math.Min(i, QuantityUnits.Length - 1);
        return Math.Round(value, 2) + QuantityUnits[i];
    }
}
