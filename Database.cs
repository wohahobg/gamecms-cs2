namespace GameCMS
{

    using Dapper;
    using Microsoft.Extensions.Logging;
    using MySqlConnector;
    using System.Data;
    using System.Reflection;


    public class Database
    {
        private static ILogger? _logger;
        private static Database? _instance;
        private static readonly object _lock = new object();
        private readonly string _connectionString = string.Empty;

        // Private constructor to prevent instance creation outside this class.
        private Database(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Initializes the Database instance.
        public static void Initialize(GameCMSConfig config, ILogger logger)
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
                            Pooling = true,
                            MinimumPoolSize = 0,
                            MaximumPoolSize = 64,
                            ConnectionReset = false,
                            CharacterSet = "utf8mb4"
                        };
                        _logger = logger;
                        _instance = new Database(builder.ConnectionString);
                    }
                }
            }
        }

        public async Task<MySqlConnection> GetConnection()
        {
            try
            {
                var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                return connection;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to connect to database: {ex.Message}");
                throw;
            }
        }

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

        public async Task<bool> Insert<T>(string table, MySqlConnection connection, T entity) where T : class
        {

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var columnNames = properties.Select(p => $"`{p.Name}`");
            var columnParameters = properties.Select(p => $"@{p.Name}");

            var columns = string.Join(", ", columnNames);
            var values = string.Join(", ", columnParameters);
            var sql = $"INSERT INTO {table} ({columns}) VALUES ({values});";
            var result = await connection.ExecuteAsync(sql, entity);
            return result > 0;
        }

        public async Task<List<T>> Query<T>(string sql, MySqlConnection connection, object? parameters = null) where T : new()
        {
            var result = (await connection.QueryAsync<T>(sql, parameters)).ToList();
            return result;
        }

        public async Task CreateTableAsync()
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS gcms_admins (
                    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
                    `server_id` BIGINT DEFAULT 0,
                    `player_name` VARCHAR(255) NOT NULL,
                    `identity` VARCHAR(50) NOT NULL,
                    `flags` LONGTEXT,
                    `groups` LONGTEXT,
                    `overrides` LONGTEXT,
                    `immunity` INT DEFAULT 0,
                    `expiry` BIGINT DEFAULT 0,
                    `created` BIGINT NOT NULL,
                    INDEX idx_identity (`identity`),
                    INDEX idx_server_id (`server_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;";

            sql += @"
                CREATE TABLE IF NOT EXISTS gcms_admin_groups (
                    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
                    `server_id` BIGINT DEFAULT 0,
                    `name` VARCHAR(255) NOT NULL,
                    `immunity` INT DEFAULT 0,
                    `flags` LONGTEXT,
                    INDEX idx_server_id (`server_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;";

            sql += @"
                CREATE TABLE IF NOT EXISTS gcms_admin_overrides (
                    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
                    `server_id` BIGINT DEFAULT 0,
                    `name` VARCHAR(255) NOT NULL,
                    `check_type` ENUM('all'),
                    `enabled` TINYINT(1),
                    `flags` LONGTEXT,
                    INDEX idx_server_id (`server_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;";

            sql += @"
                CREATE TABLE IF NOT EXISTS gcms_players_times (
                    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
                    `server_id` BIGINT DEFAULT 0,
                    `steam_id` VARCHAR(32),
                    `username` VARCHAR(255) NOT NULL,
                    `ct` INT DEFAULT 0,
                    `t` INT DEFAULT 0,
                    `spec` INT DEFAULT 0,
                    `times_joined` INT DEFAULT 1,
                    `time` BIGINT DEFAULT 0,
                    `date` DATE NOT NULL,
                    UNIQUE KEY `unique_player_per_day` (`steam_id`, `server_id`, `date`),
                    INDEX idx_steam_id (`steam_id`),
                    INDEX idx_server_id (`server_id`), 
                    INDEX idx_time (`time`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;";


            try
            {
                await using MySqlConnection connection = await GetConnection();
                await connection.ExecuteAsync(sql);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

        }
    }


}