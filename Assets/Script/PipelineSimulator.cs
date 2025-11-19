using UnityEngine;
using MySqlConnector;
using System;
using System.Data;
using System.Collections;

public class PipelineSimulator : MonoBehaviour
{
    // Database connection information
    private string connectionString = "Server=localhost;Database=unity;User=root;Password=123456;Port=3306;";

    // table name 
    private string tableName = "mechanical_arm_positions";

    // machine position
    private Vector3 leftArmPos;   // Left mechanical gripper
    private Vector3 rightArmPos;  // Right mechanical gripper

    // Status variables
    private bool isRunning = false;
    private float updateInterval = 1f;  // Update once per second
    private float timer = 0f;

    void Start()
    {
        // Initialize positions (original state on the left)
        leftArmPos = new Vector3(0, 0, 0);
        rightArmPos = new Vector3(0, 0, -3);
        
        // Test database connection
        TestConnection();

        // create datatable（if not exist）
        CreateTableIfNotExists();

        // Write initial coordinates once
        WritePositionsToDatabase();

        // Start simulation
        Debug.Log("Preparing to start pipeline simulation...");
        StartCoroutine(SimulationCoroutine());
    }

    void Update()
    {
        // Real-time timer for status display
        if (isRunning)
        {
            timer += Time.deltaTime;
        }
    }

    /// <summary>
    /// Test database connection
    /// </summary>
    private void TestConnection()
    {
        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();
                Debug.Log("<color=green>数据库连接成功！</color>");
            }
            catch (MySqlException ex)
            {
                Debug.LogError("<color=red>数据库连接失败: </color>" + ex.Message);
                if (ex.Number == 1045) Debug.LogWarning("提示：可能是用户名或密码错误");
                if (ex.Number == 1049) Debug.LogWarning("提示：数据库不存在");
                if (ex.Number == 1042) Debug.LogWarning("提示：无法连接到服务器");
            }
        }
    }

    /// <summary>
    /// 如果数据表不存在则创建
    /// </summary>
    private void CreateTableIfNotExists()
    {
        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();
                string createTableQuery = $@"
                    CREATE TABLE IF NOT EXISTS {tableName} (
                        id INT AUTO_INCREMENT PRIMARY KEY,
                        timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        arm_type ENUM('left', 'right') NOT NULL,
                        x FLOAT NOT NULL,
                        y FLOAT NOT NULL,
                        z FLOAT NOT NULL,
                        status VARCHAR(50) NOT NULL
                    )";

                MySqlCommand cmd = new MySqlCommand(createTableQuery, connection);
                cmd.ExecuteNonQuery();
                Debug.Log($"Table '{tableName}' is ready");
            }
            catch (MySqlException ex)
            {
                Debug.LogError("<color=red>创建数据表失败: </color>" + ex.Message);
            }
        }
    }

    /// <summary>
    /// Write current positions to database
    /// </summary>
    private void WritePositionsToDatabase()
    {
        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();

                // Write left mechanical gripper position
                string leftQuery = $@"INSERT INTO {tableName} (arm_type, x, y, z, status) 
                                     VALUES ('left', @x, @y, @z, @status)";
                MySqlCommand leftCmd = new MySqlCommand(leftQuery, connection);
                leftCmd.Parameters.AddWithValue("@x", leftArmPos.x);
                leftCmd.Parameters.AddWithValue("@y", leftArmPos.y);
                leftCmd.Parameters.AddWithValue("@z", leftArmPos.z);
                leftCmd.Parameters.AddWithValue("@status", GetArmStatus(leftArmPos));
                leftCmd.ExecuteNonQuery();

                // Write right mechanical gripper position
                string rightQuery = $@"INSERT INTO {tableName} (arm_type, x, y, z, status) 
                                      VALUES ('right', @x, @y, @z, @status)";
                MySqlCommand rightCmd = new MySqlCommand(rightQuery, connection);
                rightCmd.Parameters.AddWithValue("@x", rightArmPos.x);
                rightCmd.Parameters.AddWithValue("@y", rightArmPos.y);
                rightCmd.Parameters.AddWithValue("@z", rightArmPos.z);
                rightCmd.Parameters.AddWithValue("@status", GetArmStatus(rightArmPos));
                rightCmd.ExecuteNonQuery();

                // Console output current positions
                Debug.Log($"[{DateTime.Now:HH:mm:ss}] Left gripper: X={leftArmPos.x}, Y={leftArmPos.y}, Z={leftArmPos.z} | Right gripper: X={rightArmPos.x}, Y={rightArmPos.y}, Z={rightArmPos.z}");
            }
            catch (MySqlException ex)
            {
                Debug.LogError("<color=red>Failed to write to database: </color>" + ex.Message);
            }
        }
    }

    /// <summary>
    /// Get mechanical gripper status description
    /// </summary>
    private string GetArmStatus(Vector3 position)
    {
        if (position.y < 0) return "Lowered state";
        if (position.y == 0) return "Raised state";
        return "Moving";
    }

    /// <summary>
    /// Simulate left mechanical gripper lowering
    /// </summary>
    private IEnumerator LowerLeftArm()
    {
        // Lowering process (y from 0 to -5)
        while (leftArmPos.y > -5)
        {
            leftArmPos = new Vector3(leftArmPos.x, leftArmPos.y - 1, leftArmPos.z);
            WritePositionsToDatabase();
            yield return new WaitForSeconds(updateInterval);
        }

        // Ensure precise arrival at target position
        leftArmPos = new Vector3(leftArmPos.x, -5, leftArmPos.z);
        WritePositionsToDatabase();

        // Stop for 2 seconds
        Debug.Log("Left gripper has lowered, stopping for 2 seconds...");
        yield return new WaitForSeconds(2f);
    }

    /// <summary>
    /// Simulate right mechanical gripper lowering
    /// </summary>
    private IEnumerator LowerRightArm()
    {
        // Lowering process (y from 0 to -5)
        while (rightArmPos.y > -5)
        {
            rightArmPos = new Vector3(rightArmPos.x, rightArmPos.y - 1, rightArmPos.z);
            WritePositionsToDatabase();
            yield return new WaitForSeconds(updateInterval);
        }

        // Ensure precise arrival at target position
        rightArmPos = new Vector3(rightArmPos.x, -5, rightArmPos.z);
        WritePositionsToDatabase();

        // Stop for 2 seconds
        Debug.Log("Right gripper has lowered, stopping for 2 seconds...");
        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// Simulate left mechanical gripper raising
    /// </summary>
    private IEnumerator RaiseLeftArm()
    {
        // Raising process (y from -5 to 0)
        while (leftArmPos.y < 0)
        {
            leftArmPos = new Vector3(leftArmPos.x, leftArmPos.y + 1, leftArmPos.z);
            WritePositionsToDatabase();
            yield return new WaitForSeconds(updateInterval);
        }

        // Ensure precise arrival at target position
        leftArmPos = new Vector3(leftArmPos.x, 0, leftArmPos.z);
        WritePositionsToDatabase();
    }

    /// <summary>
    /// Simulate right mechanical gripper raising
    /// </summary>
    private IEnumerator RaiseRightArm()
    {
        // Raising process (y from -5 to 0)
        while (rightArmPos.y < 0)
        {
            rightArmPos = new Vector3(rightArmPos.x, rightArmPos.y + 1, rightArmPos.z);
            WritePositionsToDatabase();
            yield return new WaitForSeconds(updateInterval);
        }

        // Ensure precise arrival at target position
        rightArmPos = new Vector3(rightArmPos.x, 0, rightArmPos.z);
        WritePositionsToDatabase();
    }

    /// <summary>
    /// Simulate mechanical gripper Z-axis movement
    /// </summary>
    private IEnumerator MoveAlongZAxis(float targetZLeft, float targetZRight)
    {
        Debug.Log("Starting Z-axis movement...");

        // Calculate distance to move
        float zDiffLeft = targetZLeft - leftArmPos.z;
        float zDiffRight = targetZRight - rightArmPos.z;
        int steps = Mathf.Max(Mathf.Abs((int)zDiffLeft), Mathf.Abs((int)zDiffRight));
        float stepSizeLeft = zDiffLeft / steps;
        float stepSizeRight = zDiffRight / steps;

        // Move step by step
        for (int i = 0; i < steps; i++)
        {
            leftArmPos = new Vector3(leftArmPos.x, leftArmPos.y, leftArmPos.z + stepSizeLeft);
            rightArmPos = new Vector3(rightArmPos.x, rightArmPos.y, rightArmPos.z + stepSizeRight);
            WritePositionsToDatabase();
            yield return new WaitForSeconds(updateInterval);
        }

        // Ensure precise arrival at target position
        leftArmPos = new Vector3(leftArmPos.x, leftArmPos.y, targetZLeft);
        rightArmPos = new Vector3(rightArmPos.x, rightArmPos.y, targetZRight);
        WritePositionsToDatabase();
    }

    /// <summary>
    /// Complete simulation process coroutine
    /// </summary>
    private IEnumerator SimulationCoroutine()
    {
        isRunning = true;
        Debug.Log("Starting pipeline simulation process!");

        // Phase 1: Initial position operations
        Debug.Log("\n===== Phase 1 =====");
        // Left mechanical gripper lowering
        Debug.Log("Left mechanical gripper starting to lower...");
        yield return LowerLeftArm();
        // Left mechanical gripper raising
        Debug.Log("Left mechanical gripper starting to raise...");
        yield return RaiseLeftArm();

        // Wait one second
        yield return new WaitForSeconds(1f);

        // Phase 2: Move to middle position
        Debug.Log("\n===== Move to middle position =====");
        yield return MoveAlongZAxis(-11, -14);

        // Right mechanical gripper lowering
        Debug.Log("Right mechanical gripper starting to lower...");
        yield return LowerRightArm();
        // Right mechanical gripper raising
        Debug.Log("Right mechanical gripper starting to raise...");
        yield return RaiseRightArm();

        // Wait one second
        yield return new WaitForSeconds(1f);

        // Phase 3: Move to end position
        Debug.Log("\n===== Move to end position =====");
        yield return MoveAlongZAxis(-27, -30);

        // Left mechanical gripper lowering
        Debug.Log("Left mechanical gripper starting to lower...");
        yield return LowerLeftArm();
        // Left mechanical gripper raising
        Debug.Log("Left mechanical gripper starting to raise...");
        yield return RaiseLeftArm();

        // Wait one second
        yield return new WaitForSeconds(1f);

        // Return to initial position
        Debug.Log("\n===== Return to initial position =====");
        yield return MoveAlongZAxis(0, -3);

        // Simulation complete
        Debug.Log("\n===== Simulation process complete =====");
        Debug.Log($"Total time: {timer:F2} seconds");
        isRunning = false;
    }
}

