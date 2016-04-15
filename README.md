edge-sql-maxpower
=======

This is a SQL compiler for edge.js branched from [edge-sql](https://github.com/tjanczuk/edge-sql "edge-sql"). It allows accessing databases from Node.js using [Edge.js](https://github.com/tjanczuk/edge "Edge.js") through ADO.NET 
At the moment two kinds of databases are supported: mySql and 

This contains some major improvements  as:
#### 
* support for **multiple result set**: they are returned as [ {meta:[column names], rows:[[..]]}, {meta:[column names], rows:[[..]]}, ....} 
* support for DateTime values
* support for **transactions** having the ability to open and close a connection keeping it open in the while
* support for null values 
* **callback** option. When a callback is not specified, output is in the form {meta:[column names], rows:[[array of values]]}. When a callback is specified meta and rows are given separately, with packets of max 'packetsize' rows of data. If packetsize is not specified, each resultset will be returned with a call to the callback function 
* support for Decimal values
* support for **any kind of command**, via a cmd parameter. If it is 'nonquery', a (.net) *ExecuteNonQuery* is runned, otherwise a (.net) *ExecuteReaderAsync* 
* support for smallint (Int16) values
* it is possible to give **connectionString** as parameter
* it is possible to give a **connection handler** as parameter. The connection handler can be obtained via an open(connectionString) command and used in subsequent operations.  
* additional commands provided through cmd parameter are:  open, close, nonquery. "nonquery" commands are useful for updating db, where the **result is the number of rows affected**. This value is returned in "**rowcount**" output field.

**
####

For any other information: read the code :)

**In all the examples, a [JQuery Deferred](https://github.com/jaubourg/jquery-deferred-for-node) is used.**
    
    var Deferred = require("JQDeferred");
    
# How to #
## open a connection ##
   
    function edgeOpen(adoString) {
      var def =  Deferred(),	
    	edgeOpenInternal = edge.func('sql-maxpower',
    	  { source: 'open', 
    		connectionString: adoString,
    		cmd: 'open'
      	});
      edgeOpenInternal({}, 
    	function (error, result) {
    		var i;
    		if (error) {
      			def.reject(error);
      			return;
    		}
    		if (result) {      		
				//result is a handler to a sql connection	
      			def.resolve(result);  
      			return;
    		}
    		def.reject('shouldnt reach here');
      	});
      return def.promise();
    };


## close a connection ##
    function edgeClose(handler) {
      var def =  Deferred(),
    	edgeCloseInternal = edge.func('sql-maxpower',
      		{ handler: handler,
    			source:'close',
    			cmd: 'close'
      		});
      	edgeCloseInternal({}, function (error, result) {
    		if (error) {
      			def.reject(error);
      			return;
    		}   
    		def.resolve(null);
      	});
      return def.promise();
    };

## Execute a simple sql command (one resultset) ##

    /**
     * Executes a sql command and returns a Deferred that will be resolved with the resultset.
     * @method simpleQuery
     * @param {string} query
     * @param {string} connectionString
     * @returns  {[array of plain objects]} 
     */
    function simpleQuery(query, connectionString) {
      var edgeQuery = edge.func('sql-maxpower', 
				{connectionString: connectionString, source: query}),
    	def =  Deferred();
      	edgeQuery({}, function (error, result) {
    		if (error) {
      			def.reject(error);
      			return;
    		}
			def.resolve(simpleObjectify(result[0].meta, result[0].rows );
      	});
      	return def.promise();
    };
    
This example makes use of a function, simpleObjectify, to transform raw-data coming from sql-server into plain objects:

    /**
     * simplified objectifier having an array of column names for first argument
     * @private
     * @param {Array} colNames
     * @param {Array} rows
     * @returns {Array}
     */
    function simpleObjectify(colNames, rows) {
      var colLength = colNames.length,
      	rowLength = rows.length,
    	result = [],
    	rowIndex = rowLength,
    	colIndex,
    	value,
    	row;
      	while( --rowIndex >=0){
    		value={};
    		row = rows[rowIndex];
    		colIndex = colLength;
    		while(--colIndex >=0){
      			value[colNames[colIndex]]= row[colIndex];
    		}
    		result[rowIndex] = value;
      	}
      	return result;
    }

for example you could use simply use this function this way:

	
    simpleQuery('select * from orders', connectionString)
    .done(function(result){
		//do something with the result
    })
    .fail(function(err){
		//show error
    });


    
## Execute a generic sql command returning more than one resultset ##

    /**
     *  Executes a sql command and returns a deferred that will be resolved with an array of all the resultset. 
     * @method multipleQuery
     * @param {string} query
     * @param {string} connectionString
     * @returns a Deferred that will be resolved with [ [array of plain objects], [array of plain objects].. *	one 	for each resultset ]  
     */
    function multipleQuery(query, connectionString) {
       var edgeQuery = edge.func('sql-maxpower', 
				{connectionString: connectionString, source: query}),
    	def =  Deferred();
      	edgeQuery({}, function (error, result) {
    		if (error) {
	      		def.reject(error);
      			return;
    		}
    		var res = [];
    		for (i=0; i< result.length; i++){
	    		res.push(simpleObjectify(result[i].meta, result[i].rows));
      		}
			def.resolve(res);
      	});
      	return def.promise();
    };



## Execute a generic sql command returning more than one resultset using a connection handler ##
	 /**
     * Executes a sql command and returns a deferred that will be resolved with an array of all the resultset. 
     * @method multipleQueryHandler
     * @param {string} query
     * @param {int} handler
     * @returns a Deferred that will be resolved with [ [array of plain objects], [array of plain objects].. *	one 	for each resultset ]  
     */
    function multipleQueryHandler(query, handler) {
       var edgeQuery = edge.func('sql-maxpower', 
				{handler: handler, source: query}),
    	def =  Deferred();
      	edgeQuery({}, function (error, result) {
    		if (error) {
	      		def.reject(error);
      			return;
    		}
    		var res = [];
    		for (i=0; i< result.length; i++){
	    		res.push(simpleObjectify(result[i].meta, result[i].rows));
      		}
			def.resolve(res);
      	});
      	return def.promise();
    };



## Execute a generic sql command returning more than one resultset, being notified one resultset at a time ##
    /**
     * Executes a sql command and returns a Deferred that will be notified with all resultset, one at a time.
     * @method multipleQueryHandlerNotify
     * @param {string} query
     * @param {int} connection handler
     * @returns {*}  calls the callback with  all resultset
     */
    function multipleQueryHandlerNotify(query, handler) {
		var def =  Deferred(),    		
    		lastMeta,
    		callback = function (data, resCallBack) {
				if(data.resolve){
                	def.resolve();
	                return;
    	        }
        		if (data.meta) {
	                lastMeta = data.meta;
    	        } else {
                	def.notify(simpleObjectify(lastMeta, data.rows));
            	}
           	},
       		edgeQuery = edge.func('sql-maxpower', 
					{handler: handler, source: query, callback:callback });
    		
      		edgeQuery({}, function (error, result) {
    			if (error) {
	      			def.reject(error);
      				return;
    			}
         	});
      		return def.promise();
    };

    
## Execute a batch of sql commands ##
    /**
     * Executes a series of sql update/insert/delete commands
     * @method updateBatch
     * @param {string} query batch of sql commands to execute
     * @param {int} handler handler for the connection or connection string
     * @returns {*}
     */
    function updateBatch(query,handler) {
		var edgeQuery = edge.func('sql-maxpower', 
					{handler: handler, cmd:'nonquery', source: query, callback:callback });
		    def =  Deferred();
      	edgeQuery({}, function (error, result) {
    		if (error) {
      			def.reject(error);
      			return;
    		}
    		def.resolve(result);
      	});
      	return def.promise();
    };


## Get n rows at a time from a query ##

    
    /**
     * Gets data packets row at a time
     * @method queryPackets
     * @param {string} query
     * @param {number} packSize
     * @param {string} connectionString 
     * @returns {*}
     */
    function queryPackets(query, packSize, connectionString) {
      var def =  Deferred(),
    	packetSize = packSize || 0,
    	lastMeta,
    	callback = function (data, resCallBack) {
      		if (data.meta) {
      			lastMeta = data.meta;
    		} else {
      			def.notify(simpleObjectify(lastMeta, data.rows));
    			}
      		}
    	},
    	edgeQuery = edge.func('sql-maxpower', 
			{source: query, callback: callback, packetSize: packetSize, 
				connectionString:connectionString});

      edgeQuery({}, function (error, result) {
    		if (error) {
      			def.reject(error);
      			return;
    		}
       		def.resolve();
      });
      return def.promise();
    };
    


See [jsSqlServerDriver](https://github.com/gaelazzo/jsSqlServerDriver/blob/master/src/jsSqlServerDriver.js ) or [mySqlDriver](https://github.com/gaelazzo/jsSqlServerDriver/blob/master/src/jsMySqlDriver.js) for examples of using this library.
