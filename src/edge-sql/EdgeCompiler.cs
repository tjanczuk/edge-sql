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
        else
        {
            throw new InvalidOperationException("Unsupported type of SQL command. Only select, insert, update, and delete are supported.");
        }
    }

    private void AddParamaters(SqlCommand command, IDictionary<string, object> parameters)
    {
        if (parameters != null)
        {
            foreach (KeyValuePair<string, object> parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Key, parameter.Value);
            }
        }
    }

    private async Task<object> ExecuteQuery(string connectionString, string commandString, IDictionary<string, object> parameters)
    {
        var rows = new List<object>();

        using (var connection = new SqlConnection(connectionString))
        {
            using (var command = new SqlCommand(commandString, connection))
            {
                this.AddParamaters(command, parameters);
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    var record = (IDataRecord)reader;
                    while (await reader.ReadAsync())
                    {
                        var dataObject = new ExpandoObject() as IDictionary<string, Object>;
                        var resultRecord = new object[record.FieldCount];
                        record.GetValues(resultRecord);

                        for (int i = 0; i < record.FieldCount; i++)
                        {
                            var type = record.GetFieldType(i);
                            if (type == typeof(byte[]) || type == typeof(char[]))
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
                }
            }
        }

        return rows;
    }

    private async Task<object> ExecuteNonQuery(string connectionString, string commandString, IDictionary<string, object> parameters)
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
}
