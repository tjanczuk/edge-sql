exports.getCompiler = function () {
	if (typeof(TRAVIS)!== 'undefined'){
		return process.env.EDGE_SQL_NATIVE || (__dirname + '\\edge-sql-mono.dll');
	}
	return process.env.EDGE_SQL_NATIVE || (__dirname + '\\edge-sql.dll');
};
