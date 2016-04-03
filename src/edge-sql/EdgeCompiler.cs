using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using System.Data.Common;
using MySql.Data.MySqlClient;

public abstract class genericConnection {
    public abstract  Task<object> executeQuery(string commandString,IDictionary<string, object> parameters,
            int packetSize, int timeout, Func<object, Task<object>> callback = null);

    public abstract Task<object> executeQueryConn(string commandString,
        int packetSize, int timeout, Func<object, Task<object>> callback = null);

    public abstract Task<object> executeNonQuery(string commandString, int timeOut);
    public abstract Task<object> executeNonQueryConn(string commandString, int timeOut);


    public abstract Task<object> open();
    public abstract  void close();
}

public class sqlServerConn : genericConnection {
    private SqlConnection connection;
    private string connectionString;

    public sqlServerConn(string connectionString) {
        this.connectionString = connectionString;
    }

    public override async Task<object> open() {
        connection = new SqlConnection(connectionString);
        try {
            await connection.OpenAsync();
            return true;
        }
        catch {
            throw new Exception("Error opening connection");
        }
    }

    public override void close() {
        connection.Close();
    }

    public override async Task<object> executeQuery(string commandString, IDictionary<string, object> parameters,
        int packetSize, int timeout, Func<object, Task<object>> callback = null) {

        using (SqlConnection tempConn = new SqlConnection(connectionString)) {
            await tempConn.OpenAsync();
            return internalExecuteQuery(tempConn, commandString, packetSize, timeout, callback);
        }
    }

    public override Task<object> executeQueryConn(string commandString,
        int packetSize, int timeout, Func<object, Task<object>> callback = null) {
        return internalExecuteQuery(connection, commandString, packetSize, timeout, callback);
    }

    public override async Task<object> executeNonQuery(string commandString, int timeOut) {
        using (SqlConnection tempConn = new SqlConnection(connectionString)) {
            await tempConn.OpenAsync();
            return internalExecuteNonQuery(tempConn, commandString, timeOut);
        }
    }

    public override async Task<object> executeNonQueryConn(string commandString, int timeOut) {
        return internalExecuteNonQuery(connection, commandString, timeOut);

    }

    void addParameters(SqlCommand command, IDictionary<string, object> parameters) {
        if (parameters != null) {
            foreach (KeyValuePair<string, object> parameter in parameters) {

                command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
            }
        }
    }

    private async Task<object> internalExecuteQuery(SqlConnection connection, string commandString,
        int packetSize, int timeout, Func<object, Task<object>> callback = null) {
        List<object> rows = new List<object>();
        using (SqlCommand command = new SqlCommand(commandString, connection)) {
            using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.Default)) {
                do {
                    Dictionary<string, object> res;
                    object[] fieldNames = new object[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++) {
                        fieldNames[i] = reader.GetName(i);
                    }
                    //rows.Add(fieldNames);
                    res = new Dictionary<string, object>();
                    List<object> localRows = new List<object>();
                    res["meta"] = fieldNames;
                    if (callback != null) {
                        callback(res);
                        res = new Dictionary<string, object>();
                    }

                    res["rows"] = localRows;
                    IDataRecord record = (IDataRecord) reader;
                    while (await reader.ReadAsync()) {
                        object[] resultRecord = new object[record.FieldCount];
                        record.GetValues(resultRecord);
                        for (int i = 0; i < record.FieldCount; i++) {
                            Type type = record.GetFieldType(i);
                            if (resultRecord[i] is System.DBNull) {
                                resultRecord[i] = null;
                            }
                            else if (type == typeof (Int16) || type == typeof (UInt16)) {
                                resultRecord[i] = Convert.ToInt32(resultRecord[i]);
                            }
                            else if (type == typeof (Decimal)) {
                                resultRecord[i] = Convert.ToDouble(resultRecord[i]);
                            }
                            else if (type == typeof (byte[]) || type == typeof (char[])) {
                                resultRecord[i] = Convert.ToBase64String((byte[]) resultRecord[i]);
                            }
                            else if (type == typeof (Guid)) //|| type == typeof(DateTime)
                            {
                                resultRecord[i] = resultRecord[i].ToString();
                            }
                            else if (type == typeof (IDataReader)) {
                                resultRecord[i] = "<IDataReader>";
                            }
                        }
                        localRows.Add(resultRecord);
                        if (packetSize > 0 && localRows.Count == packetSize && callback != null) {
                            callback(res);
                            localRows = new List<object>();
                            res = new Dictionary<string, object>();
                            res["rows"] = localRows;
                        }
                    }

                    if (callback != null) {
                        if (localRows.Count > 0) {
                            callback(res);
                        }
                    }
                    else {
                        rows.Add(res);
                    }
                } while (await reader.NextResultAsync());

            }
        }

        return rows;
    }

    private async Task<object> internalExecuteNonQuery(SqlConnection connection, string commandString, int timeOut) {
        SqlCommand command = new SqlCommand(commandString, connection);
        command.CommandTimeout = timeOut;
        using (command) {
            //this.AddParameters(command, parameters);
            var res = new Dictionary<string, object> {["rowcount"] = await command.ExecuteNonQueryAsync()};
            return res;
        }
    }
}

public class mySqlConn : genericConnection {
    private MySqlConnection connection;
    private string connectionString;

    public mySqlConn(string connectionString) {
        this.connectionString = connectionString;
    }

    public override async Task<object> open() {
        connection = new MySqlConnection(connectionString);
        try {
            await connection.OpenAsync();
            return true;
        }
        catch {
            throw new Exception("Error opening connection");
        }
    }

    public override void close() {
        connection.Close();
    }

    public override async Task<object> executeQuery(string commandString, IDictionary<string, object> parameters,
        int packetSize, int timeout, Func<object, Task<object>> callback = null) {

        using (MySqlConnection tempConn = new MySqlConnection(connectionString)) {
            await tempConn.OpenAsync();
            return await internalExecuteQuery(tempConn, commandString, packetSize, timeout, callback);
        }
    }

    public override async Task<object> executeQueryConn(string commandString,
        int packetSize, int timeout, Func<object, Task<object>> callback = null) {
        return await internalExecuteQuery(connection, commandString, packetSize, timeout, callback);
    }

    public override async Task<object> executeNonQuery(string commandString, int timeOut) {
        using (MySqlConnection tempConn = new MySqlConnection(connectionString)) {
            await tempConn.OpenAsync();
            return await internalExecuteNonQuery(tempConn, commandString, timeOut);
        }
    }

    public override async Task<object> executeNonQueryConn(string commandString, int timeOut) {
        return await internalExecuteNonQuery(connection, commandString, timeOut);

    }

    void addParameters(MySqlCommand command, IDictionary<string, object> parameters) {
        if (parameters != null) {
            foreach (KeyValuePair<string, object> parameter in parameters) {

                command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
            }
        }
    }

    private async Task<object> internalExecuteQuery(MySqlConnection connection, string commandString,
        int packetSize, int timeout, Func<object, Task<object>> callback = null) {
        List<object> rows = new List<object>();
        using (MySqlCommand command = new MySqlCommand(commandString, connection)) {
            using (DbDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.Default)) {
                do {
                    Dictionary<string, object> res;
                    object[] fieldNames = new object[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++) {
                        fieldNames[i] = reader.GetName(i);
                    }
                    //rows.Add(fieldNames);
                    res = new Dictionary<string, object>();
                    List<object> localRows = new List<object>();
                    res["meta"] = fieldNames;
                    if (callback != null) {
                        callback(res);
                        res = new Dictionary<string, object>();
                    }

                    res["rows"] = localRows;
                    IDataRecord record = (IDataRecord)reader;
                    while (await reader.ReadAsync()) {
                        object[] resultRecord = new object[record.FieldCount];
                        record.GetValues(resultRecord);
                        for (int i = 0; i < record.FieldCount; i++) {
                            Type type = record.GetFieldType(i);
                            if (resultRecord[i] is System.DBNull) {
                                resultRecord[i] = null;
                            }
                            else if (type == typeof(Int16) || type == typeof(UInt16)) {
                                resultRecord[i] = Convert.ToInt32(resultRecord[i]);
                            }
                            else if (type == typeof(Decimal)) {
                                resultRecord[i] = Convert.ToDouble(resultRecord[i]);
                            }
                            else if (type == typeof(byte[]) || type == typeof(char[])) {
                                resultRecord[i] = Convert.ToBase64String((byte[])resultRecord[i]);
                            }
                            else if (type == typeof(Guid)) //|| type == typeof(DateTime)
                            {
                                resultRecord[i] = resultRecord[i].ToString();
                            }
                            else if (type == typeof(IDataReader)) {
                                resultRecord[i] = "<IDataReader>";
                            }
                        }
                        localRows.Add(resultRecord);
                        if (packetSize > 0 && localRows.Count == packetSize && callback != null) {
                            callback(res);
                            localRows = new List<object>();
                            res = new Dictionary<string, object>();
                            res["rows"] = localRows;
                        }
                    }

                    if (callback != null) {
                        if (localRows.Count > 0) {
                            callback(res);
                        }
                    }
                    else {
                        rows.Add(res);
                    }
                } while (await reader.NextResultAsync());

            }
        }

        return rows;
    }

    private async Task<object> internalExecuteNonQuery(MySqlConnection connection, string commandString, int timeOut) {
        MySqlCommand command = new MySqlCommand(commandString, connection);
        command.CommandTimeout = timeOut;
        using (command) {
            //this.AddParameters(command, parameters);
            var res = new Dictionary<string, object> {["rowcount"] = await command.ExecuteNonQueryAsync() };
            return res;
        }
    }
}
public class EdgeCompiler
{
    private static Dictionary<int, genericConnection> allConn =  new Dictionary<int, genericConnection> ();
    private static int nConn = 0;
    private static int totConn = 0;
    
    private static int AddConnection(genericConnection sqlConn) {
        lock (allConn) {
            nConn += 1;
            totConn += 1;
            allConn[nConn] = sqlConn;
            return nConn;
        }
    }

    private static void RemoveConnection(int handler) {
        lock (allConn) {
            allConn[handler] = null;
            totConn -= 1;
            if (totConn == 0) {
                nConn = 0;
                allConn.Clear();                
            }
        }
    }
    
    public Func<object, Task<object>> CompileFunc(IDictionary<string, object> parameters)
    {
        string command = ((string)parameters["source"]).TrimStart();
        string connectionString = Environment.GetEnvironmentVariable("EDGE_SQL_CONNECTION_STRING");
        object tmp;
        if (parameters.TryGetValue("connectionString", out tmp))
        {
            connectionString = (string)tmp;
        }
        int handler = -1;
        if (parameters.TryGetValue("handler", out tmp)) {
            handler = (int)tmp;
        }
        int timeOut = 30;
        if (parameters.TryGetValue("timeout", out tmp)){
            timeOut = (int)tmp;
        }

        Func<object, Task<object>> callback = null;
        tmp = null;
        if (parameters.TryGetValue("callback", out tmp)) {
            callback = (Func<object, Task<object>>)tmp;
        }
        string driver = "sqlServer";
        tmp = null;
        if (parameters.TryGetValue("driver", out tmp)) {
            driver = (string)tmp;
        }

        tmp = null;
        if (parameters.TryGetValue("cmd", out tmp)) {
            var cmd = ((string)tmp).ToLower().Trim();
            if (cmd == "open") {
                    return async (o) => { return await openConnection(connectionString,driver); };
            }
            if (cmd == "close") {
                return async (o) => { closeConnection(handler); return 0; };
            }
            if (cmd == "nonquery") {
                if (handler >= 0) {
                        genericConnection conn = allConn[handler];
                        return async (o) => { return await conn.executeNonQueryConn(command, timeOut); };
                }
                else {
                        genericConnection conn = dispatchConn(connectionString, driver);
                        return async (o) => { return await conn.executeNonQuery(command, timeOut); };
                }
            }
        }
     
        int packetSize=0;
        object defPSize = 0;
        if (parameters.TryGetValue("packetSize", out defPSize)) {
            packetSize = Convert.ToInt32(defPSize);
        }
        if (handler != -1) {
            genericConnection conn = allConn[handler];
            return async (o) => { return await conn.executeQueryConn(command, packetSize, timeOut, callback); };
        }

        return async (queryParameters) => {
            genericConnection conn = dispatchConn(connectionString, driver);            
            return await conn.executeQuery(command, (IDictionary<string, object>)queryParameters,
                packetSize, timeOut, callback);
        };
    }


    genericConnection dispatchConn(string connectionString, string driver) {
        if (driver == "sqlServer") {
            return new sqlServerConn(connectionString);
        }
        if (driver == "mySql") {
            return new mySqlConn(connectionString);
        }
        return null;
    }

    async Task<object> openConnection(string connectionString, string driver) {
        genericConnection gen = dispatchConn(connectionString, driver);        

        try {
            await gen.open();
            return AddConnection(gen);
        }
        catch {
            throw new Exception("Error opening connection");
        }
       



    }

    void closeConnection(int handler) {
        allConn[handler].close();        
        RemoveConnection(handler);   
    }
    
    
   

 

   




}
