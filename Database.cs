using Microsoft.Data.Sqlite;

public class Database
{
    private string _dbPath = "Data Source=bot_data.db";

    public Database()
    {
        using var connection = new SqliteConnection(_dbPath);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                UserId INTEGER PRIMARY KEY,
                IsVerified BOOLEAN DEFAULT 0,
                ImageQuota INTEGER DEFAULT 0
            )";
        command.ExecuteNonQuery();
    }

    public bool IsUserVerified(long userId)
    {
        using var connection = new SqliteConnection(_dbPath);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT IsVerified FROM Users WHERE UserId = $id";
        command.Parameters.AddWithValue("$id", userId);
        var result = command.ExecuteScalar();
        return result != null && Convert.ToBoolean(result);
    }

    public void VerifyUser(long userId)
    {
        using var connection = new SqliteConnection(_dbPath);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO Users (UserId, IsVerified, ImageQuota) VALUES ($id, 1, 3)";
        command.Parameters.AddWithValue("$id", userId);
        command.ExecuteNonQuery();
    }

    public int GetQuota(long userId)
    {
        using var connection = new SqliteConnection(_dbPath);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT ImageQuota FROM Users WHERE UserId = $id";
        command.Parameters.AddWithValue("$id", userId);
        var result = command.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public void UseQuota(long userId)
    {
        using var connection = new SqliteConnection(_dbPath);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET ImageQuota = ImageQuota - 1 WHERE UserId = $id AND ImageQuota > 0";
        command.Parameters.AddWithValue("$id", userId);
        command.ExecuteNonQuery();
    }
}