edge-sql-maxpower
=======

This is a SQL compiler for edge.js branched from [edge-sql](https://github.com/tjanczuk/edge-sql "edge-sql"). It allows accessing databases from Node.js using [Edge.js](https://github.com/tjanczuk/edge "Edge.js") through ADO.NET 

This contains some major improvements  as:
#### 
* support for DateTime values
* support for **transaction** having the ability to open and close a connection keeping it open in the while
* support for null values 
* **packet-ized** output option ouput is in the form {meta:[column names], rows:[[array of values]]} when packet size=0, otherwise meta and rows are given separately, with packets of max 'packet size' rows of data 
* support for Decimal values
* support for **any kind of command** (as Ryan says 'just do it'), via a cmd parameter. If it is 'nonquery', a (.net) *ExecuteNonQuery* is runned, otherwise a (.net) *ExecuteReaderAsync* 
* support for **multiple result set**: they are returned as [ {meta:[column names], rows:[[..]]}, {meta:[column names], rows:[[..]]}, ....}
* support for smallint (Int16) values
* it is possible to give **connectionString** as parameter
* it is possible to give a **connection handler** as parameter 
* additional commands provided through cmd parameter are:  open, close, nonquery. "nonquery" commands are useful for updating db, where the sql **result is the number of rows affected**. This value is returned in "**rowcount**" output field.

**
####

For any other information: read the code :)

In all the examples, a JQuery Deferred is used.

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
      			def.resolve(that);  
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
    
## Execute a generic sql command ##

    /**
     * Executes a sql command and returns all sets of results. Each Results is given via a notify or resolve
     * @method queryBatch
     * @param {string} query
     * @param {boolean} [raw] if true, data are left in raw state and will be objectified by the client
     * @param handler handler for the connection or connection string
     * @returns {*}  a sequence of {[array of plain objects]} or {meta:[column names],rows:[arrays of raw data]}
     */
    function queryBatch(query, raw, handler) {
      var edgeQuery = edge.func(
    	'sql-maxpower', 
    	_.extend({source: query},handler)),
    	def =  Deferred();
      	edgeQuery({}, function (error, result) {
    	if (error) {
      		def.reject(error);
      		return;
    	}
    	var i;
    	for (i=0; i< result.length-1; i++){
      		if (raw){
    		def.notify(result[i]);
    		} else {
    		def.notify(simpleObjectify(result[i].meta, result[i].rows));
      		}
    	}
    	if (raw) {
      		def.resolve(result[i]);
    	} else {
      		def.resolve(simpleObjectify(result[i].meta, result[i].rows));
    	}
      	});
      	return def.promise();
    };
    
This example uses a function, simpleObjectify, to transform raw-data coming from sql-server into plain objects:

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


## Execute a batch of sql commands ##
    /**
     * Executes a series of sql update/insert/delete commands
     * @method updateBatch
     * @param query batch of sql commands to execute
     * @param handler handler for the connection or connection string
     * @returns {*}
     */
    function updateBatch(query,handler) {
      var edgeQuery = edge.func('sql-maxpower', 
			_.extend({source: query, cmd:'nonquery'},
      		handler)),
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
     * @param {boolean} [raw=false]
     * @param {number} [packSize=0]
     * @param handler handler for the connection or connection string
     * @returns {*}
     */
    function queryPackets(query, raw, packSize, handler) {
      var def =  Deferred(),
    	packetSize = packSize || 0,
    	lastMeta,
    	currentSet = -1,
    	table,
    	callback = function (data, resCallBack) {
      		if (data.meta){
    			currentSet += 1;
      		}
      		data.set = currentSet;
      		if (raw) {
    			def.notify(data);
      		} else {
    			if (data.meta) {
      				lastMeta = data.meta;
    			} else {
      				def.notify({rows: simpleObjectify(lastMeta, data.rows),
							set: currentSet});
    			}
      		}
    	},
    edgeQuery = edge.func('sql-maxpower', 
		_.extend({source: query, callback: callback, packetSize: packetSize},
      		handler));
      edgeQuery({}, function (error, result) {
    		var i;
    		if (error) {
      			def.reject(error);
      			return;
    		}
       		def.resolve();
      });
      return def.promise();
    };
    
for example you can use simply use this function as

    queryPackets('select * from orders', false, 0, connectionString)
    .done(function(result){
    })
    .fail(function(err){
    });


## Get N rows one at a time ##
    /**
     * Gets a table and returns each SINGLE row by notification. Could eventually return more than a table indeed
     * For each table read emits a {meta:[column descriptors]} notification, and for each row of data emits a
     *   if raw= false: {row:object read from db}
     *   if raw= true: {row: [array of values read from db]}
    
     * @method queryLines
     * @param {string} query
     * @param {boolean} [raw=false]
     * @param handler handler for the connection or connection string
     * @returns {*}
     */
    function queryLines(query, raw, handler) {
      var def =  Deferred(),
    	lastMeta,
    	objMaker,
    	callback = function(data, resCallBack) {
      		if (data.rows) {
    			if (raw) {
      				def.notify({row: data.rows[0]});
    			} else {
      				def.notify({row: simpleObjectifier(lastMeta,data.rows[0])});
    			}
     	 } else {
    		lastMeta = data.meta;
    		def.notify(data);
     	 }
    	},
       edgeQuery = edge.func('sql-maxpower',
    	 _.extend({source: query,callback: callback,packetSize: 1},
    	handler));
    	edgeQuery({}, function (error, result) {
      		var i;
      		if (error) {
    			def.reject(error);
    			return;
      		}
      		if (result.length === 0) {
    			def.resolve();
    			return;
      		}
      		def.reject('shouldnt reach here');
    	});
      	return def.promise();
    	};
    
    
    
![](https://travis-ci.org/gaelazzo/edge-sql.svg?branch=master) 
