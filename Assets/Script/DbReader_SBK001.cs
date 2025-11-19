using UnityEngine;
using MySqlConnector;
using System; // For DateTime, DBNull
using System.Data; // For ConnectionState

public class DbReader_SBK001 : MonoBehaviour
{
    // Database connection information
    public string server = "192.168.249.50";
    public string database = "sbk001";
    public string user = "root";
    public string password = "123456";
    public string tableName = "oee_data_history";

    void Start()
    {
        // Process: test connection first, then read data
        TestDbConnection();
        ReadAllTableData();
    }

    /// <summary>
    /// Test database connection
    /// </summary>
    private void TestDbConnection()
    {
        Debug.Log("<color=cyan>=== Testing database connection ===</color>");

        string connStr = $"Server={server};Database={database};User={user};Password={password};Port=3306;";

        using (MySqlConnection connection = new MySqlConnection(connStr))
        {
            try
            {
                connection.Open();
                Debug.Log("<color=green>✅ Database connection successful!</color>");
                Debug.Log($"Server: {server}:3306 | Database: {database}");
            }
            catch (MySqlException ex)
            {
                Debug.LogError("<color=red>❌ Database connection failed:</color>" + ex.Message);
                if (ex.Number == 1045) Debug.LogWarning("⚠️ Hint: Username (root) or password incorrect");
                if (ex.Number == 1049) Debug.LogWarning("⚠️ Hint: Database name 'sbk001' does not exist");
                if (ex.Number == 1042) Debug.LogWarning("⚠️ Hint: Cannot connect to server (check IP {server} and port 3306)");
                if (ex.Number == 2003) Debug.LogWarning("⚠️ Hint: MySQL server not started or network unreachable");
            }
        }
        Debug.Log("");
    }

    /// <summary>
    /// Read all fields data from table (latest 10 rows)
    /// </summary>
    private void ReadAllTableData()
    {
        Debug.Log("<color=cyan>=== Reading table data ===</color>");
        Debug.Log($"Table: {tableName} | Range: latest 10 rows");

        string connStr = $"Server={server};Database={database};User={user};Password={password};Port=3306;";

        using (MySqlConnection connection = new MySqlConnection(connStr))
        {
            try
            {
                connection.Open();

                string query = $"SELECT * FROM {tableName} ORDER BY id DESC LIMIT 10";

                MySqlCommand cmd = new MySqlCommand(query, connection);
                MySqlDataReader reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    Debug.Log("<color=orange>⚠️ No data in table</color>");
                    reader.Close();
                    return;
                }

                int dataCount = 0;
                Debug.Log("\n<color=yellow>=== Data list ===</color>");

                while (reader.Read())
                {
                    dataCount++;
                    Debug.Log($"\n[Row {dataCount}]");

                    string name = reader["name"].ToString();
                    string value = reader["value"].ToString();
                    string timestamp = reader["timestamp"].ToString();

                    Debug.Log($"Name: {name}, Value: {value}, Time: {timestamp}");
                }

                reader.Close();
                Debug.Log($"\n<color=green>✅ Data read complete! Total {dataCount} rows</color>");
            }
            catch (MySqlException ex)
            {
                Debug.LogError("<color=red>❌ Data read failed:</color>" + ex.Message);
                if (ex.Number == 1146) Debug.LogWarning($"⚠️ Hint: Table '{tableName}' does not exist");
                if (ex.Number == 1054) Debug.LogWarning("⚠️ Hint: Field not found (check table structure)");
            }
        }
    }
}