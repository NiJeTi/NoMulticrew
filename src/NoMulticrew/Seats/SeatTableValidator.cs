namespace NoMulticrew.Seats;

internal static class SeatTableValidator
{
    public static SeatTable Validate(SeatTable table)
    {
        var result = table;

        foreach (var jsonKey in table.Keys.ToList())
        {
            var seats = table.SeatsFor(jsonKey);

            if (seats.Count > 1)
            {
                Plugin.Logger.LogError(
                    $"Seat table: '{jsonKey}' declares {seats.Count} crew seats. "
                    + "Only one crew seat is supported; the whole airframe is disabled. "
                    + "Multi-seat crews are a deliberate future step, not a config option."
                );

                result = result.Without(jsonKey);
            }
        }

        return result;
    }
}
