exports.getCompiler = function () {
	return process.env.EDGE_SQL_NATIVE || (__dirname + '\\edge-sql.dll');
};
