using MySqlConnector;
using System.Data;
using System.Reflection;


namespace Main;

public class Database
{
    private static Database? _instance;
    private static readonly object _lock = new object();
    private MySqlConnection? _connection = null;
    private readonly string _connectionString = string.Empty;

    // Private constructor to prevent instance creation outside this class.
    private Database(string connectionString)
    {
        _connectionString = connectionString;
        try
        {
            _connection = new MySqlConnection(_connectionString);
            _connection.Open();
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    // Initializes the Database instance.
    public static void Initialize(GameCMSConfig config)
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    var builder = new MySqlConnectionStringBuilder
                    {
                        Server = config.database.host,
                        Database = config.database.name,
                        UserID = config.database.username,
                        Password = config.database.password,
                        Port = config.database.port,
                        CharacterSet = "utf8mb4"
                    };

                    _instance = new Database(builder.ConnectionString);
                }
            }
        }
    }

    private void EnsureConnectionOpen()
    {
        if (_connection?.State != ConnectionState.Open)
        {
            _connection = new MySqlConnection(_connectionString);
            _connection.Open();
        }
    }


    // Provides global access to the singleton instance.
    public static Database Instance
    {
        get
        {
            if (_instance == null)
            {
                throw new Exception("Database is not initialized. Call Initialize() first.");
            }
            return _instance;
        }
    }

    public T MapReaderToType<T>(MySqlDataReader reader) where T : new()
    {
        var item = new T();
        var properties = typeof(T).GetProperties();

        foreach (var property in properties)
        {
            try
            {
                if (!reader.IsDBNull(reader.GetOrdinal(property.Name)))
                {
                    var value = reader[property.Name];
                    Type propertyType = property.PropertyType;
                    Type nullableUnderlyingType = Nullable.GetUnderlyingType(propertyType)!;
                    if (nullableUnderlyingType != null)
                    {
                        var convertedValue = Convert.ChangeType(value, nullableUnderlyingType);
                        property.SetValue(item, convertedValue);
                    }
                    else
                    {
                        property.SetValue(item, Convert.ChangeType(value, propertyType));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameCMS.ORG] Error mapping property {property.Name}: {ex.Message}");
            }
        }

        return item;
    }


    public async Task<bool> Insert<T>(string table, T entity) where T : class
    {
        EnsureConnectionOpen();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var columnNames = properties.Select(p => $"`{p.Name}`");
        var columnParameters = properties.Select(p => $"@{p.Name}");

        var columns = string.Join(", ", columnNames);
        var values = string.Join(", ", columnParameters);
        var sql = $"INSERT INTO {table} ({columns}) VALUES ({values});";
        
        using var command = new MySqlCommand(sql, _connection);

        foreach (var property in properties)
        {
            var value = property.GetValue(entity);
            command.Parameters.AddWithValue($"@{property.Name}", value ?? DBNull.Value);
        }

        try
        {
            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during the insert operation: {ex.Message}");
            return false;
        }
    }

    public async Task<List<T>> Query<T>(string sql, Dictionary<string, object>? parameters = null) where T : new()
    {
        EnsureConnectionOpen();
        using var command = new MySqlCommand(sql, _connection);
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
            }
        }

        using (var reader = await command.ExecuteReaderAsync())
        {
            var result = new List<T>();
            while (await reader.ReadAsync())
            {
                var item = MapReaderToType<T>(reader);
                result.Add(item);
            }
            return result;
        }
    }

    public async Task<int> Update(string sql, Dictionary<string, object>? parameters = null)
    {
        try
        {
            EnsureConnectionOpen();
            using var command = new MySqlCommand(sql, _connection);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }
            return await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }


    public async Task<int> Delete(string sql, Dictionary<string, object>? parameters = null)
    {
        try
        {
            EnsureConnectionOpen();
            using var command = new MySqlCommand(sql, _connection);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }
            return await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }

    }

    public async Task CreateTableAsync()
    {
        string sql = @"
                CREATE TABLE IF NOT EXISTS gcms_admins (
                    id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    server_id INT DEFAULT 0,
                    player_name VARCHAR(255) NOT NULL,
                    identity VARCHAR(50) NOT NULL,
                    flags LONGTEXT DEFAULT '[]',
                    groups LONGTEXT DEFAULT '[]',
                    overrides LONGTEXT DEFAULT '[]',
                    immunity INT DEFAULT 0,
                    expiry BIGINT DEFAULT 0,
                    created BIGINT NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

        sql += @"
                CREATE TABLE IF NOT EXISTS gcms_admin_groups (
                    id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    server_id INT DEFAULT 0,
                    name VARCHAR(255) NOT NULL,
                    immunity INT DEFAULT 0,
                    flags LONGTEXT DEFAULT '[]'
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

        sql += @"
                CREATE TABLE IF NOT EXISTS gcms_admin_overrides (
                    id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    server_id INT DEFAULT 0,
                    name VARCHAR(255) NOT NULL,
                    check_type ENUM('all'),
                    enabled TINYINT(1),
                    flags LONGTEXT DEFAULT '[]'
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

        sql += @"
        CREATE TABLE IF NOT EXISTS gcms_k4systemranks (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            server_id INT DEFAULT 0,
            name VARCHAR(255) NOT NULL,
            tag VARCHAR(255),
            image VARCHAR(255) DEFAULT '',
            color VARCHAR(255),
            points BIGINT DEFAULT 0
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";
        try
        {
            using var command = new MySqlCommand(sql, _connection);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

}

