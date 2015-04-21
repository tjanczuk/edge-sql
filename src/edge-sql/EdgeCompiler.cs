using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

public class EdgeCompiler
{
    public Func<object, Task<object>> CompileFunc(IDictionary<string, object> parameters)
    {
        string command = ((string)parameters["source"]).TrimStart();
        string connectionString = Environment.GetEnvironmentVariable("EDGE_SQL_CONNECTION_STRING");
        object tmp;
        if (parameters.TryGetValue("connectionString", out tmp))
        {
            connectionString = (string)tmp;
        }

        if (command.StartsWith("select ", StringComparison.InvariantCultureIgnoreCase))
        {
            return async (queryParameters) =>
            {
                return await this.ExecuteQuery(connectionString, command, (IDictionary<string, object>)queryParameters);
            };
        }
        else if (command.StartsWith("insert ", StringComparison.InvariantCultureIgnoreCase)
            || command.StartsWith("update ", StringComparison.InvariantCultureIgnoreCase)
            || command.StartsWith("delete ", StringComparison.InvariantCultureIgnoreCase))
        {
            return async (queryParameters) =>
            {
                return await this.ExecuteNonQuery(connectionString, command, (IDictionary<string, object>)queryParameters);
            };
        }
        else if (command.StartsWith("exec ", StringComparison.InvariantCultureIgnoreCase)
                || command.StartsWith("execute ", StringComparison.InvariantCultureIgnoreCase))
        {
            return async (queryParameters) =>
            {
                return await this.ExecuteStoredProcedure(connectionString, command, (IDictionary<string, object>)queryParameters);
            };
        }
        else
        {
            throw new InvalidOperationException("Unsupported type of SQL command. Only select, insert, update, delete, and exec / execute are supported.");
        }
    }

    void AddParamaters(SqlCommand command, IDictionary<string, object> parameters)
    {
        if (parameters != null)
        {
            foreach (KeyValuePair<string, object> parameter in parameters)
            {
                if (parameter.Key == "returnParameter")
                {
                    String returnParameterName = "@" + parameter.Value.ToString();
                    command.Parameters.Add(returnParameterName, SqlDbType.NVarChar, -1);
                    command.Parameters[returnParameterName].Direction = ParameterDirection.Output;
                }
                else
                {
                    command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
                }
            }
        }
    }

    async Task<object> ExecuteQuery(string connectionString, string commandString, IDictionary<string, object> parameters)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            using (var command = new SqlCommand(commandString, connection))
            {
                return await this.ExecuteQuery(parameters, command, connection);
            }
        }
    }

    async Task<object> ExecuteQuery(IDictionary<string, object> parameters, SqlCommand command, SqlConnection connection)
    {
        List<object> rows = new List<object>();
        this.AddParamaters(command, parameters);
        await connection.OpenAsync();
        using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection))
        {
            IDataRecord record = (IDataRecord)reader;
            while (await reader.ReadAsync())
            {
                var dataObject = new ExpandoObject() as IDictionary<string, Object>;
                var resultRecord = new object[record.FieldCount];
                record.GetValues(resultRecord);

                for (int i = 0; i < record.FieldCount; i++)
                {
                    Type type = record.GetFieldType(i);
                    if (resultRecord[i] is System.DBNull)
                    {
                        resultRecord[i] = null;
                    }
                    else if (type == typeof(byte[]) || type == typeof(char[]))
                    {
                        resultRecord[i] = Convert.ToBase64String((byte[])resultRecord[i]);
                    }
                    else if (type == typeof(Guid) || type == typeof(DateTime))
                    {
                        resultRecord[i] = resultRecord[i].ToString();
                    }
                    else if (type == typeof(IDataReader))
                    {
                        resultRecord[i] = "<IDataReader>";
                    }

                    dataObject.Add(record.GetName(i), resultRecord[i]);
                }

                rows.Add(dataObject);
            }

            return rows;
        }
    }

    async Task<object> ExecuteNonQuery(string connectionString, string commandString, IDictionary<string, object> parameters)
    {
        using (var connection = new SqlConnection(connectionString))
        {
            using (var command = new SqlCommand(commandString, connection))
            {
                this.AddParamaters(command, parameters);
                await connection.OpenAsync();
                return await command.ExecuteNonQueryAsync();
            }
        }
    }

    async Task<object> ExecuteStoredProcedure(
        string connectionString,
        string commandString,
        IDictionary<string, object> parameters)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            commandString = commandString.Substring(commandString.IndexOf(' ') + 1, (commandString.Length - 1) - commandString.IndexOf(' '));
            commandString = commandString.TrimStart();
            if (commandString.Contains("@"))
            {
                commandString = commandString.Substring(0, commandString.IndexOf('@') - 1);
            }
            commandString = commandString.Trim();
            SqlCommand command = new SqlCommand(commandString, connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            using (command)
            {
                if (parameters != null)
                {
                    this.AddParamaters(command, parameters);

                    object tmp;
                    if (parameters.TryGetValue("returnParameter", out tmp))
                    {
                        string returnValue = "1";
                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                        String returnKey = "@" + (string)tmp;
                        returnValue = command.Parameters[returnKey].Value.ToString();
                        return returnValue;
                    }
                }
                return await this.ExecuteQuery(parameters, command, connection);
            }
        }
    }
}
