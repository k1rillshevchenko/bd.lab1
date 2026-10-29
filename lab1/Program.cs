using System;

string connStr = "Host=localhost;Port=5432;Database=lab1;Username=postgres;Password=123qwe123";

await using (var uow = new UnitOfWork(connStr))
{
    var vehicles = await uow.Vehicles.GetActiveVehiclesAsync();
    foreach (var v in vehicles)
    {
        Console.WriteLine($"Авто: {v.Model} ({v.LicensePlate}) - Статус: {v.Status}");
    }

    int newTripId = await uow.Trips.CreateTripAsync(
        vehicleId: 1,
        driverId: 1,
        routeId: 1,
        departure: DateTime.Now,
        userId: 1
    );
    Console.WriteLine($"Створено рейс ID: {newTripId}");

    await uow.CommitAsync();
}