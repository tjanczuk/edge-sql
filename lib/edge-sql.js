exports.getCompiler = function () {
	console.log(process.env.EDGE_SQL_NATIVE);
	console.log(__dirname);
	if (typeof(TRAVIS)!== 'undefined'){
		return process.env.EDGE_SQL_NATIVE || (__dirname + '\\edge-sql-mono.dll');
	}
	return process.env.EDGE_SQL_NATIVE || (__dirname + '\\edge-sql.dll');
};
