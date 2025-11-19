using UnityEngine;
using MySqlConnector;
using System.Data;
// Using System namespace (to avoid DateTime and DBNull conflicts)
using System;

public class DbManager : MonoBehaviour
{
    // Database connection information
    private string connectionString = "Server=localhost;Database=unity;User=root;Password=123456;Port=3306;";

    void Start()
    {
        TestConnection(); // Test connection
        ReadData();       // Start reading data

        // Test inserting data, including name, value, automatically assign current time
        InsertData("NewRobot_001", 999);

        Debug.Log("\n--- After inserting data, read again ---");
        ReadData(); // Read again to verify data
    }

    /// <summary>
    /// Test database connection
    /// </summary>
    public void TestConnection()
    {
        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();
                Debug.Log("<color=green>Database connection successful</color>");
            }
            catch (MySqlException ex)
            {
                Debug.LogError("<color=red>Database connection failed: </color>" + ex.Message);
                // Common connection error prompts (with error codes)
                if (ex.Number == 1045) Debug.LogWarning("Tip: Check username and password");
                if (ex.Number == 1049) Debug.LogWarning("Tip: Database does not exist, confirm if it's 'unity'");
                if (ex.Number == 1042) Debug.LogWarning("Tip: Unable to connect to MySQL server, confirm if port 3306 is correct");
            }
        }
    }

    /// <summary>
    /// Read robet table data, handling NULL time fields
    /// </summary>
    public void ReadData()
    {
        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();
                // Query all fields, sort by id in descending order, take top 10
                string query = "SELECT id, name, value, time FROM robet ORDER BY id DESC LIMIT 10";
                MySqlCommand cmd = new MySqlCommand(query, connection);
                MySqlDataReader reader = cmd.ExecuteReader();

                Debug.Log("\n<color=blue>--- Starting to read robet table ---</color>");

                if (!reader.HasRows)
                {
                    Debug.Log("No data in 'robet' table");
                    reader.Close();
                    return;
                }

                // Loop through each record
                while (reader.Read())
                {
                    // 1. Get id, not null, direct conversion
                    int id = reader.GetInt32("id");

                    // 2. Get name, if null, judge again and convert
                    string name = reader.IsDBNull(reader.GetOrdinal("name")) ? "Not set" : reader.GetString("name");

                    // 3. Get value, if null, set to 0
                    int value = reader.IsDBNull(reader.GetOrdinal("value")) ? 0 : reader.GetInt32("value");

                    // 4. Get time, handle NULL case
                    string timeStr;
                    if (reader.IsDBNull(reader.GetOrdinal("time")))
                    {
                        timeStr = "No time set";
                    }
                    else
                    {
                        DateTime time = reader.GetDateTime("time");
                        timeStr = time.ToString("yyyy-MM-dd HH:mm:ss"); // Format time display
                    }

                    // Format output with color, separate fields, clear display
                    Debug.LogFormat("ID: <color=yellow>{0}</color> | Name: {1} | Value: {2} | Time: {3}",
                        id, name, value, timeStr);
                }

                reader.Close();
                Debug.Log("<color=yellow>--- Data reading complete ---</color>");
            }
            catch (MySqlException ex)
            {
                Debug.LogError("<color=red>��ȡ����ʧ��: </color>" + ex.Message);
                if (ex.Number == 1146) Debug.LogWarning("��ʾ���� 'robet' �����ڣ�ȷ�ϱ����Ƿ���ȷ��");
            }
        }
    }

    /// <summary>
    /// �� robet ���������ݣ��Զ���䵱ǰʱ�䣬���� time Ϊ NULL��
    /// </summary>
    /// <param name="robotName">���������ƣ���Ӧ���� name �ֶΣ�</param>
    /// <param name="robotValue">��������ֵ����Ӧ���� value �ֶΣ�</param>
    public void InsertData(string robotName, int robotValue)
    {
        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();

                // ����ʱ�ֶ�ָ�� time Ϊ��ǰʱ�䣨���� NULL��
                string query = @"INSERT INTO robet (name, value, time) 
                                 VALUES (@robotName, @robotValue, NOW())";
                // NOW() �� MySQL �������Զ���ȡ��ǰ������ʱ��

                MySqlCommand cmd = new MySqlCommand(query, connection);

                // ��������ѯ����ֹ SQL ע�룬������ֵ��
                cmd.Parameters.AddWithValue("@robotName", string.IsNullOrEmpty(robotName) ? DBNull.Value : (object)robotName);
                cmd.Parameters.AddWithValue("@robotValue", robotValue);

                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    Debug.LogFormat("<color=green>�ɹ����� {0} �����ݣ�</color> ���ƣ�{1}����ֵ��{2}",
                        rowsAffected, robotName, robotValue);
                }
            }
            catch (MySqlException ex)
            {
                Debug.LogError("<color=red>��������ʧ��: </color>" + ex.Message);
            }
        }
    }
}