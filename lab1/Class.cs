using System.Data;
using Npgsql;
using NpgsqlTypes;
    
public record VehicleViewDto(int VehicleId, string Vin, string LicensePlate, string Model, string TypeName, string Status);
public record TripViewDto(int TripId, string LicensePlate, string DriverName, string StartPoint, string EndPoint, string Status);

public interface IVehicleRepository
{
    Task<IEnumerable<VehicleViewDto>> GetActiveVehiclesAsync();
    Task SoftDeleteAsync(int vehicleId, int userId);
}

public interface ITripRepository
{
    Task<IEnumerable<TripViewDto>> GetActiveTripsAsync();
    Task<int> CreateTripAsync(int vehicleId, int driverId, int routeId, DateTime departure, int userId);
    Task CompleteTripAsync(int tripId, DateTime arrival, int userId);
}

public class VehicleRepository : IVehicleRepository
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;

    public VehicleRepository(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public async Task<IEnumerable<VehicleViewDto>> GetActiveVehiclesAsync()
    {
        var list = new List<VehicleViewDto>();
        using var cmd = new NpgsqlCommand("SELECT vehicle_id, vin, license_plate, model, type_name, status FROM vw_active_vehicles", _connection, _transaction);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new VehicleViewDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5)
            ));
        }
        return list;
    }

    public async Task SoftDeleteAsync(int vehicleId, int userId)
    {
        using var cmd = new NpgsqlCommand("CALL sp_soft_delete_vehicle(@p_id, @p_uid)", _connection, _transaction);
        cmd.Parameters.AddWithValue("p_id", vehicleId);
        cmd.Parameters.AddWithValue("p_uid", userId);
        await cmd.ExecuteNonQueryAsync();
    }
}

public class TripRepository : ITripRepository
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;

    public TripRepository(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public async Task<IEnumerable<TripViewDto>> GetActiveTripsAsync()
    {
        var list = new List<TripViewDto>();
        using var cmd = new NpgsqlCommand("SELECT trip_id, license_plate, driver_name, start_point, end_point, status FROM vw_active_trips_detail", _connection, _transaction);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TripViewDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5)
            ));
        }
        return list;
    }

    public async Task<int> CreateTripAsync(int vehicleId, int driverId, int routeId, DateTime departure, int userId)
    {
        using var cmd = new NpgsqlCommand("CALL sp_create_trip(@v_id, @d_id, @r_id, @dep, @u_id, @t_id)", _connection, _transaction);
        cmd.Parameters.AddWithValue("v_id", vehicleId);
        cmd.Parameters.AddWithValue("d_id", driverId);
        cmd.Parameters.AddWithValue("r_id", routeId);
        cmd.Parameters.AddWithValue("u_id", userId);
        cmd.Parameters.Add(new NpgsqlParameter("dep", NpgsqlTypes.NpgsqlDbType.Timestamp) { Value = departure });

        var outParam = new NpgsqlParameter("t_id", NpgsqlDbType.Integer)
        {
            Direction = ParameterDirection.InputOutput,
            Value = DBNull.Value
        };
        cmd.Parameters.Add(outParam);

        await cmd.ExecuteNonQueryAsync();
        return (int)outParam.Value;
    }

    public async Task CompleteTripAsync(int tripId, DateTime arrival, int userId)
    {
        using var cmd = new NpgsqlCommand("CALL sp_complete_trip(@t_id, @arr, @u_id)", _connection, _transaction);
        cmd.Parameters.AddWithValue("t_id", tripId);
        cmd.Parameters.AddWithValue("arr", arrival);
        cmd.Parameters.AddWithValue("u_id", userId);
        await cmd.ExecuteNonQueryAsync();
    }
}

public interface IUnitOfWork : IAsyncDisposable
{
    IVehicleRepository Vehicles { get; }
    ITripRepository Trips { get; }
    Task CommitAsync();
    Task RollbackAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection _connection;
    private NpgsqlTransaction _transaction;

    public IVehicleRepository Vehicles { get; }
    public ITripRepository Trips { get; }

    public UnitOfWork(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
        _connection.Open();
        _transaction = _connection.BeginTransaction();

        Vehicles = new VehicleRepository(_connection, _transaction);
        Trips = new TripRepository(_connection, _transaction);
    }

    public async Task CommitAsync()
    {
        try
        {
            await _transaction.CommitAsync();
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
        finally
        {
            _transaction = await _connection.BeginTransactionAsync();
        }
    }

    public async Task RollbackAsync()
    {
        await _transaction.RollbackAsync();
        _transaction = await _connection.BeginTransactionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
        }
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}