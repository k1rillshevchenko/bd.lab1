using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FleetController : ControllerBase
    {
        private readonly string _connectionString =
            "Host=localhost;Port=5432;Database=lab1;Username=postgres;Password=123qwe123";

        [HttpGet("active-vehicles")]
        public async Task<IActionResult> GetVehicles()
        {
            await using var uow = new UnitOfWork(_connectionString);
            var vehicles = await uow.Vehicles.GetActiveVehiclesAsync();
            return Ok(vehicles);
        }

        [HttpGet("active-trips")]
        public async Task<IActionResult> GetTrips()
        {
            await using var uow = new UnitOfWork(_connectionString);
            var trips = await uow.Trips.GetActiveTripsAsync();
            return Ok(trips);
        }

        [HttpPost("create-trip")]
        public async Task<IActionResult> CreateTrip(int vehicleId, int driverId, int routeId, int userId)
        {
            await using var uow = new UnitOfWork(_connectionString);
            int newTripId = await uow.Trips.CreateTripAsync(vehicleId, driverId, routeId, DateTime.UtcNow, userId);
            await uow.CommitAsync();
            return Ok(new { TripId = newTripId, Status = "Created" });
        }
    }
}