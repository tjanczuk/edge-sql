using System;
using edge_sql;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Data;
using MySql;
using MySql.Data.MySqlClient;

namespace test
{
	class MainClass
	{
		//const string connectionGood = "Server=localhost;Database=test;Port=3306;User ID=user1;Password=user1;Pooling=false";
		const string connectionGood = "server=127.0.0.1;database=test;Port=3306;uid=user1;pwd=user1;Pooling=false;"+
			"Connection Timeout=600;Allow User Variables=True;";
		static bool open_direct(){
			IDbConnection dbcon;
			dbcon = new MySqlConnection(connectionGood);
			dbcon.Open();
			dbcon.Close ();
			return true;
		}

		static bool test_open(){
			Dictionary<string,object>  p = new Dictionary<string,object>();
			p["connectionString"]= connectionGood;
			p ["source"] = "open";
			p["cmd"]="open";
			p ["driver"] = "mySql";
			try {
				EdgeCompiler ec = new EdgeCompiler ();
				var  t = ec.CompileFunc(p);
				var r = t.Invoke(0).Result;
				return true;
			}
			catch(Exception e) {
				Console.WriteLine ("errore aprendo la connessione"+e.GetBaseException());
				return false;
			}
		}
		public static int  Main (string[] args)
		{
			Dictionary <string,bool>  allTest = new Dictionary<string,bool> ();
			allTest["test direct open"]= open_direct();
			allTest["test open"]= test_open();
			int nError=0;
			Console.WriteLine ("Edge-sql test");
			foreach(string k in allTest.Keys){				
				if (allTest[k]){
					Console.WriteLine(k+": passed");
				}
				else {
					Console.WriteLine(k+":failed");
					nError++;
				}
			}
			if (nError==0){
				Console.WriteLine ("All test passed.");
			}
			else {
				Console.WriteLine ("N. of fails:"+nError);
			}
			return nError;
		}
	}
}
