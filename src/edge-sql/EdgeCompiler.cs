using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using System.Data.Common;
using MySql.Data.MySqlClient;


    public class EdgeCompiler {
        private static Dictionary<int, genericConnection> allConn = new Dictionary<int, genericConnection>();
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

        public Func<object, Task<object>> CompileFunc(IDictionary<string, object> parameters) {
            string connectionString = Environment.GetEnvironmentVariable("EDGE_SQL_CONNECTION_STRING");
            object tmp;
            if (parameters.TryGetValue("connectionString", out tmp)) {
                connectionString = (string)tmp;
            }
            int handler = -1;
            if (parameters.TryGetValue("handler", out tmp)) {
                handler = (int)tmp;
            }
            int timeOut = 30;
            if (parameters.TryGetValue("timeout", out tmp)) {
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

            string command = "";
            if (parameters.TryGetValue("source", out tmp)) {
                command = tmp.ToString().TrimStart();
            }

            tmp = null;
            if (parameters.TryGetValue("cmd", out tmp)) {
                var cmd = ((string)tmp).ToLower().Trim();
                if (cmd == "open") {
                    return async (o) => {
                        return await openConnection(connectionString, driver);
                    };
                }
                if (cmd == "close") {
                    closeConnection(handler);
                    return (o) => {
                        return Task.FromResult((object)null);
                    };
                }
                if (cmd == "nonquery") {
                    if (handler >= 0) {
                        genericConnection conn = allConn[handler];
                        return async (o) => {
                            return await conn.executeNonQueryConn(command, timeOut);
                        };
                    }
                    else {
                        genericConnection conn = dispatchConn(connectionString, driver);
                        return async (o) => {
                            return await conn.executeNonQuery(command, timeOut);
                        };
                    }
                }
            }

            int packetSize = 0;
            object defPSize = 0;
            if (parameters.TryGetValue("packetSize", out defPSize)) {
                packetSize = Convert.ToInt32(defPSize);
            }
            if (handler != -1) {
                genericConnection conn = allConn[handler];
                return async (o) => {
                    return await conn.executeQueryConn(command, packetSize, timeOut, callback);
                };
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


/// <summary>
/// Exposes a generic interface to access any database
/// </summary>
public abstract class genericConnection {

    /// <summary>
    /// Executes a generic sql command that can return multiple tables. There are two main use case. 
    /// If a callback is NOT given, output is in the form:
    ///   [  result1, result2,...resultN] where result(n) is the n-th table 
    ///  Any result table is in the form:
    ///   {meta: [fieldName1, fieldName2,...fieldNameM], rows:[{value1, value2,...valueM}, {value1,...valueM},..]}
    /// If a callback IS given, and packet size is NOT specified, the callback is called n+1 times, one for each returned table,
    ///  with {meta: [fieldName1, fieldName2,...fieldNameM]}, {rows:rows:[{value1, value2,...valueM}, {value1,...valueM},..]} given 
    ///  in subsequent calls.
    /// At the end of all resultsets the callback is called with {resolve:1} to notify there is no more data to process.
    /// If a callback IS given, and packet size IS specified, the callback can be called more than one time for each result, any
    ///   time with no more than packet size rows. This can be useful if you expect to read 1 million rows and don't want to 
    ///   wait for the last row to start process them. 
    /// </summary>
    /// <param name="commandString"></param>
    /// <param name="parameters"></param>
    /// <param name="packetSize"></param>
    /// <param name="timeout"></param>
    /// <param name="callback"></param>
    /// <returns></returns>
    public abstract Task<object> executeQuery(string commandString, IDictionary<string, object> parameters,
        int packetSize, int timeout, Func<object, Task<object>> callback = null);

    /// <summary>
    /// Same as executeQuery, but the connection is opened before running command and then immediately closed.
    /// </summary>
    /// <param name="commandString"></param>
    /// <param name="packetSize"></param>
    /// <param name="timeout"></param>
    /// <param name="callback"></param>
    /// <returns></returns>
    public abstract Task<object> executeQueryConn(string commandString,
        int packetSize, int timeout, Func<object, Task<object>> callback = null);

    /// <summary>
    /// Executes a command an returns number of affected rows in an object {rowcount: number}
    /// </summary>
    /// <param name="commandString"></param>
    /// <param name="timeOut"></param>
    /// <returns></returns>
    public abstract Task<object> executeNonQuery(string commandString, int timeOut);

    /// <summary>
    /// Same as executeNonQuery,  but the connection is opened before running command and then immediately closed.
    /// </summary>
    /// <param name="commandString"></param>
    /// <param name="timeOut"></param>
    /// <returns></returns>
    public abstract Task<object> executeNonQueryConn(string commandString, int timeOut);

    /// <summary>
    /// Opens the connection. This is meant to be used in cojunction with a series of executeQuery calls. 
    /// </summary>
    /// <returns></returns>
    public abstract Task<object> open();

    /// <summary>
    /// Closes and releases the connection. It's important to call this function as soon as the connection is not needed anymore.
    /// </summary>
    public abstract void close();
}

public class sqlServerConn : genericConnection {
		private SqlConnection connection;
		private string connectionString;

		public sqlServerConn (string connectionString) {
			this.connectionString = connectionString;
		}

		public override async Task<object> open () {
			connection = new SqlConnection (connectionString);
			try {
				await connection.OpenAsync ();
				return true;
			} catch {
				throw new Exception ("Error opening connection");
			}
		}

		public override void close () {
			connection.Close ();
		}

		public override async Task<object> executeQuery (string commandString, IDictionary<string, object> parameters,
		                                                  int packetSize, int timeout, Func<object, Task<object>> callback = null) {

			using (SqlConnection tempConn = new SqlConnection (connectionString)) {
				await tempConn.OpenAsync ();
				return await internalExecuteQuery (tempConn, commandString, packetSize, timeout, callback);
			}
		}

		public override  async Task<object> executeQueryConn (string commandString,
		                                                 int packetSize, int timeout, Func<object, Task<object>> callback = null) {
			return await internalExecuteQuery (connection, commandString, packetSize, timeout, callback);
		}

		public override async Task<object> executeNonQuery (string commandString, int timeOut) {
			using (SqlConnection tempConn = new SqlConnection (connectionString)) {
				await tempConn.OpenAsync ();
				return await internalExecuteNonQuery (tempConn, commandString, timeOut);
			}
		}

		public override async Task<object> executeNonQueryConn (string commandString, int timeOut) {
			return await internalExecuteNonQuery (connection, commandString, timeOut);

		}

		void addParameters (SqlCommand command, IDictionary<string, object> parameters) {
			if (parameters != null) {
				foreach (KeyValuePair<string, object> parameter in parameters) {

					command.Parameters.AddWithValue (parameter.Key, parameter.Value ?? DBNull.Value);
				}
			}
		}

		private async Task<object> internalExecuteQuery (SqlConnection connection, string commandString,
		                                      int packetSize, int timeout, Func<object, Task<object>> callback = null) {
			List<object> rows = new List<object> ();
			using (SqlCommand command = new SqlCommand (commandString, connection)) {
				using (var reader = await command.ExecuteReaderAsync (CommandBehavior.Default)) {
					do {
						Dictionary<string, object> res;
						object[] fieldNames = new object[reader.FieldCount];
						for (int i = 0; i < reader.FieldCount; i++) {
							fieldNames [i] = reader.GetName (i);
						}
						//rows.Add(fieldNames);
						res = new Dictionary<string, object> ();
						List<object> localRows = new List<object> ();
						res ["meta"] = fieldNames;
						if (callback != null && packetSize>0) {
							callback (res);
							res = new Dictionary<string, object> ();
						}

						res ["rows"] = localRows;
						IDataRecord record = (IDataRecord)reader;
						while (reader.Read ()) {
							object[] resultRecord = new object[record.FieldCount];
							record.GetValues (resultRecord);
							for (int i = 0; i < record.FieldCount; i++) {
								Type type = record.GetFieldType (i);
								if (resultRecord [i] is System.DBNull) {
									resultRecord [i] = null;
								} else if (type == typeof(Int16) || type == typeof(UInt16)) {
										resultRecord [i] = Convert.ToInt32 (resultRecord [i]);
									} else if (type == typeof(Decimal)) {
											resultRecord [i] = Convert.ToDouble (resultRecord [i]);
										} else if (type == typeof(byte[]) || type == typeof(char[])) {
												resultRecord [i] = Convert.ToBase64String ((byte[])resultRecord [i]);
											} else if (type == typeof(Guid)) { //|| type == typeof(DateTime)
													resultRecord [i] = resultRecord [i].ToString ();
												} else if (type == typeof(IDataReader)) {
														resultRecord [i] = "<IDataReader>";
													}
							}
							localRows.Add (resultRecord);
							if (packetSize > 0 && localRows.Count == packetSize && callback != null) {
								callback (res);
								localRows = new List<object> ();
								res = new Dictionary<string, object> ();
								res ["rows"] = localRows;
							}
						}

						if (callback != null) {
							if (localRows.Count > 0) {
								callback (res);
							}
						} else {
							rows.Add (res);
						}
					} while (await reader.NextResultAsync ());

				}
			}
			if (callback != null) {
				var res = new Dictionary<string, object> ();
				res ["resolve"] = 1;
				callback (res);
			}
			return rows;
		}

		private async Task<object> internalExecuteNonQuery (SqlConnection connection, string commandString, int timeOut) {
			SqlCommand command = new SqlCommand (commandString, connection);
			command.CommandTimeout = timeOut;
			using (command) {
				//this.AddParameters(command, parameters);
				var res = new Dictionary<string, object> { ["rowcount" ] =  await command.ExecuteNonQueryAsync () };
				return res;
			}
		}
	}

	public class mySqlConn : genericConnection {
		private MySqlConnection connection;
		private string connectionString;

		public mySqlConn (string connectionString) {
			this.connectionString = connectionString;
		}

		public async override Task<object> open () {
			connection = new MySqlConnection (connectionString);
			try {
				await connection.OpenAsync();
				return true;
			} catch  {
				throw new Exception ("Error opening connection");
			}
		}

		public override void close () {
			connection.Close ();
		}

		public override async Task<object> executeQuery (string commandString, IDictionary<string, object> parameters,
		                                                  int packetSize, int timeout, Func<object, Task<object>> callback = null) {

			using (MySqlConnection tempConn = new MySqlConnection (connectionString)) {
				await tempConn.OpenAsync ();
				return  await internalExecuteQuery (tempConn, commandString, packetSize, timeout, callback);
			}
		}

		public override async Task<object> executeQueryConn (string commandString,
		                                                int packetSize, int timeout, Func<object, Task<object>> callback = null) {
			return await internalExecuteQuery (connection, commandString, packetSize, timeout, callback);        
		}


		public override async Task<object> executeNonQuery (string commandString, int timeOut) {
			using (MySqlConnection tempConn = new MySqlConnection (connectionString)) {
				await tempConn.OpenAsync ();
				return  await internalExecuteNonQuery (tempConn, commandString, timeOut);
			}
		}

		public override  async Task<object> executeNonQueryConn (string commandString, int timeOut) {
			return  await internalExecuteNonQuery (connection, commandString, timeOut);

		}

		void addParameters (MySqlCommand command, IDictionary<string, object> parameters) {
			if (parameters != null) {
				foreach (KeyValuePair<string, object> parameter in parameters) {

					command.Parameters.AddWithValue (parameter.Key, parameter.Value ?? DBNull.Value);
				}
			}
		}

		private async Task<object> internalExecuteQuery (MySqlConnection connection, string commandString,
		                                      int packetSize, int timeout, Func<object, Task<object>> callback = null) {
			List<object> rows = new List<object> ();
			using (MySqlCommand command = new MySqlCommand (commandString, connection)) {
				using (DbDataReader reader = await command.ExecuteReaderAsync (CommandBehavior.Default)) {
					do {
						Dictionary<string, object> res;
						object[] fieldNames = new object[reader.FieldCount];
						for (int i = 0; i < reader.FieldCount; i++) {
							fieldNames [i] = reader.GetName (i);
						}
						//rows.Add(fieldNames);
						res = new Dictionary<string, object> ();
						List<object> localRows = new List<object> ();
						res ["meta"] = fieldNames;
						if (callback != null) {
							callback (res);   //Call is voluntarily NOT awaited. So processing can be done while this thread keeps reading.              
							res = new Dictionary<string, object> ();
						}

						res ["rows"] = localRows;
						IDataRecord record = (IDataRecord)reader;
						while (reader.Read ()) {
							object[] resultRecord = new object[record.FieldCount];
							record.GetValues (resultRecord);
							for (int i = 0; i < record.FieldCount; i++) {
								Type type = record.GetFieldType (i);
								if (resultRecord [i] is System.DBNull) {
									resultRecord [i] = null;
								} else if (type == typeof(Int16) || type == typeof(UInt16)) {
										resultRecord [i] = Convert.ToInt32 (resultRecord [i]);
									} else if (type == typeof(Decimal)) {
											resultRecord [i] = Convert.ToDouble (resultRecord [i]);
										} else if (type == typeof(byte[]) || type == typeof(char[])) {
												resultRecord [i] = Convert.ToBase64String ((byte[])resultRecord [i]);
											} else if (type == typeof(Guid)) { //|| type == typeof(DateTime)
													resultRecord [i] = resultRecord [i].ToString ();
												} else if (type == typeof(IDataReader)) {
														resultRecord [i] = "<IDataReader>";
													}
							}
							localRows.Add (resultRecord);
							if (packetSize > 0 && localRows.Count == packetSize && callback != null) {
								callback (res);
								localRows = new List<object> ();
								res = new Dictionary<string, object> ();
								res ["rows"] = localRows;
							}
						}

						if (callback != null) {
							if (localRows.Count > 0) {
								callback (res);
							}
						} else {
							rows.Add (res);
						}
					} while (await reader.NextResultAsync ());

				}
			}
			if (callback != null) {
				var res = new Dictionary<string, object> ();
				res ["resolve"] = 1;
				callback (res);
			}
			return rows; 
		}

		private async Task<object> internalExecuteNonQuery (MySqlConnection connection, string commandString, int timeOut) {
			MySqlCommand command = new MySqlCommand (commandString, connection);
			command.CommandTimeout = timeOut;
			using (command) {
				//this.AddParameters(command, parameters);
				var res = new Dictionary<string, object> { ["rowcount" ] =  await command.ExecuteNonQueryAsync () };
				return res;
			}
		}
	}

	

