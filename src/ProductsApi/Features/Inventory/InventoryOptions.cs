namespace ProductsApi.Features.Inventory;

public sealed class InventoryOptions
{
    public const int MinimumReservationMinutes = 30;
    public const int MaximumReservationMinutes = 24 * 60;

    public int ReservationMinutes { get; set; } = MinimumReservationMinutes;
}
