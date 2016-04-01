using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

public class EdgeCompiler
{
    private static Dictionary<int,SqlConnection> allConn =  new Dictionary<int,SqlConnection> ();
    private static int nConn = 0;
    private static int totConn = 0;
    
    private static int AddConnection(SqlConnection sqlConn) {
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

        tmp = null;
        if (parameters.TryGetValue("cmd", out tmp)) {
            var cmd = ((string)tmp).ToLower().Trim();
            if (cmd == "open") {
                    return async (o) => { return await OpenConnection(connectionString); };
            }
            if (cmd == "close") {
                return async (o) => { CloseConnection(handler); return 0; };
            }
            if (cmd == "nonquery") {
                if (handler >= 0) {
                    return async (o) => { return await ExecuteNonQueryConn(handler, command, timeOut); };
                }
                else {
                    return async (o) => { return await ExecuteNonQuery(connectionString, command, timeOut); };
                }
            }
        }
     
        int packetSize=0;
        object defPSize = 0;
        if (parameters.TryGetValue("packetSize", out defPSize)) {
            packetSize = Convert.ToInt32(defPSize);
        }
        if (handler != -1) {
            return async (o) => { return await ExecuteQueryConn(handler, command, packetSize, timeOut, callback); };
        }

        return async (queryParameters) =>
        {
            return await this.ExecuteQuery(connectionString, command, (IDictionary<string, object>)queryParameters,
                packetSize, timeOut, callback);
        };
    }

    void AddParamaters(SqlCommand command, IDictionary<string, object> parameters)
    {
        if (parameters != null)
        {
            foreach (KeyValuePair<string, object> parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
            }
        }
    }

    async Task<object> OpenConnection(string connectionString) {
        SqlConnection connection = new SqlConnection(connectionString);
        try {
            Task t = connection.OpenAsync();
            Task.WaitAll(t);
            if (t.IsFaulted) throw new Exception();
        }
        catch {
            throw new Exception("Error opening connection");
        }
        return AddConnection(connection);
    }

    void CloseConnection(int handler) {
        SqlConnection connection = allConn[handler];
        RemoveConnection(handler);
        connection.Close();
    }
    
    
     async Task<object> ExecuteQuery(string connectionString, string commandString, IDictionary<string, object> parameters,
        int packetSize, int timeout, Func<object, Task<object>> callback = null)
    {
        List<object> rows = new List<object>();
        
        using (SqlConnection connection = new SqlConnection(connectionString)) {
            SqlCommand command = new SqlCommand(commandString, connection);
            command.CommandTimeout = timeout;
            
            using (command) {
                this.AddParamaters(command, parameters);
                await connection.OpenAsync();
                using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection)) {
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
                                else if (type == typeof(Decimal)) {
                                    resultRecord[i] = Convert.ToDouble(resultRecord[i]);
                                }
                                else if (type == typeof(byte[]) || type == typeof(char[])) {
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
                            if (packetSize > 0 && localRows.Count == packetSize && callback!=null) {
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
                    }
                    while (await reader.NextResultAsync());

                }
            }
        }

        return rows;
    }

 

    async Task<object> ExecuteQueryConn(int handler, string commandString, 
        int packetSize, int timeout, Func<object, Task<object>> callback = null) {
        List<object> rows = new List<object>();
        SqlConnection connection = allConn[handler];        
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
                    }
                    while (await reader.NextResultAsync());

                }            
        }

        return rows;
    }

    private async Task<object> ExecuteNonQueryConn(int handler, string commandString, int timeOut) {
         SqlConnection connection = allConn[handler];
        SqlCommand command = new SqlCommand(commandString, connection);
        command.CommandTimeout = timeOut;
        using (command) {
                var res = new Dictionary<string, object>();
                res["rowcount"]= await command.ExecuteNonQueryAsync();
                return res;
            }
        
    }

    private async Task<object> ExecuteNonQuery(string connectionString, string commandString , int timeOut
                /*IDictionary<string, object> parameters*/) {
        using (var connection = new SqlConnection(connectionString)) {
            SqlCommand command = new SqlCommand(commandString, connection);
            command.CommandTimeout = timeOut;
            using (command) {
                //this.AddParamaters(command, parameters);
                await connection.OpenAsync();
                var res = new Dictionary<string, object>();
                res["rowcount"] = await command.ExecuteNonQueryAsync();
                return res;
            }
        }
    }
}
