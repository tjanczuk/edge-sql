/*globals expect,describe */
'use strict';
var _ = require('lodash');
var fs = require("fs");
var path = require("path");
var edgeSql = require('../../src/edge-sql');
    
/**
 * *****************************************************************************************
 * VERY IMPORTANT VERY IMPORTANT VERY IMPORTANT VERY IMPORTANT VERY IMPORTANT VERY IMPORTANT
 * *****************************************************************************************
 * It's necessary, before start running the test, to create a file templated like:
 *  { "server": "db server address",
 *    "dbName": "database name",  //this must be an EMPTY database
 *    "user": "db user",
 *    "pwd": "db password"
 *  }
 */
    //PUT THE  FILENAME OF YOUR FILE HERE:
var configName = path.join('test', 'db.json');
var dbConfig;
if (process.env.TRAVIS){
    dbConfig = { "server": "127.0.0.1",
        "dbName": "test",
        "user": "root",
        "pwd": ""
    };
}
else {
    dbConfig = JSON.parse(fs.readFileSync(configName).toString());
}

describe('edgeSql ', function () {
    let sqlConn,dbInfo,driver;
    let sqlServer=false;
    if (sqlServer){
        /*
        "data source=" + Server +
        ";initial catalog=" + Database +
        ";User ID =" + UserDB +
        ";Password=" + PasswordDB +
        ";Application Name=" + AppName + ";" +
        "WorkStation ID =" + Environment.MachineName +
        ";Pooling=false" +
        ";Connection Timeout=300;"
         */

        dbInfo = {
            good: "data source=localhost;initial catalog=test;User ID =user1;Password=user1user1;" +
                "Pooling=False;Connection Timeout=600;",
            bad: "data source=localhost;initial catalog=test;User ID =user1;Password=x;" +
                "Pooling=False;Connection Timeout=600;"
        };
        driver = 'sqlServer';
    }
    else {
        dbInfo = {
            good: "Server=localhost;database=test;uid=user1;pwd=user1user1;" +
                "Pooling=False;Connection Timeout=600;Allow User Variables=True;",
            bad: "Server=localhost;database=test;uid=user1;pwd=x;" +
                "Pooling=False;Connection Timeout=600;Allow User Variables=True;"
        };
        driver = 'mySql';
    }
    if (process.env.TRAVIS) {
        dbInfo = {
            good: "Server=127.0.0.1;database=test;uid=root;pwd=;"+
            "Pooling=False;Connection Timeout=600;Allow User Variables=True;",
            bad:  "Server=127.0.0.1;database=test;uid=root;pwd=x;"+
            "Pooling=False;Connection Timeout=600;Allow User Variables=True;"
        };
    
    }

    function getConnection(dbCode) {
        var connString = dbInfo[dbCode];
        if (connString) {
            return new edgeSql.EdgeConnection(connString,driver);
        }
        return undefined;
    }
    
    beforeEach(function (done) {
        sqlConn = getConnection('good');
        sqlConn.open().done(function () {
            done();
        })
        .fail(function (err) {
            console.log('Error failing '+err);
            done();
        });
    }, 30000);

    afterEach(function () {
        if (sqlConn) {
            sqlConn.close();
        }
        sqlConn = null;
    });


    describe('setup dataBase', function () {
        it('should run the setup script', function (done) {
            sqlConn.run(fs.readFileSync(path.join('test', driver+'Setup.sql')).toString())
            .done(function () {
                expect(true).toBeTruthy();
                done();
            })
            .fail(function (res) {
                expect(res).toBeUndefined();
                done();
            });
        }, 30000);

    });

    describe('open', function () {

        var newSqlConn = getConnection('good');
        it('open should return a deferred', function (done) {
            newSqlConn.open()
            .done(function () {
                expect(true).toBe(true);
                newSqlConn.close();
                done();
            })
            .fail(function () {
                expect(true).toBe(true);
                done();
            });
        });

        it('open with  right credential should return a success', function (done) {
            var goodSqlConn = getConnection('good');
            goodSqlConn.open()
            .done(function () {
                expect(true).toBe(true);
                goodSqlConn.close();
                done();
            })
            .fail(function (errMess) {
                expect(errMess).toBeUndefined();
                done();
            });

        });

        it('open with bad credential should return an error', function (done) {
            var badSqlConn = getConnection('bad');
            badSqlConn.open()
            .done(function (res) {
                expect(res).toBe(undefined);
                expect(true).toBe(false);
                done();
            })
            .fail(function (errMess) {
                expect(errMess).toBeDefined();
                done();
            });

        }, 3000);

    });

    describe('various', function () {

        it('select now() should give results', function (done) {
            var sql = 'SELECT now() as currtime';
            if (driver==='sqlServer'){
                sql= 'SELECT getdate() as currtime';
            }
            sqlConn.queryBatch(sql)
            .done(function (result) {
                expect(result).toBeDefined();
                done();
            })
            .fail(function (err) {
                expect(err).toBeUndefined();
                done();
            });
        });


        it('select * from table should give results', function (done) {
            sqlConn.queryBatch('select * from customer')
            .done(function (result) {
                expect(result).toBeDefined();
                done();
            })
            .fail(function (err) {
                expect(err).toBeUndefined();
                done();
            });
        });

        it('Date should be given as objects', function (done) {
            sqlConn.queryBatch('SELECT * from customer')
            .done(function (result) {
                _(result).forEach(function (r) {
                    if (r.idcustomer) {
                        expect(r.idcustomer).toEqual(jasmine.any(Number));
                    }
                    if (r.stamp) {
                        expect(r.stamp).toEqual(jasmine.any(Date));
                    }
                });
                done();
            })
            .fail(function (err) {
                expect(err).toBeUndefined();
                done();
            });
        });


        it('notify should be called from queryRaw when multiple result got (two select)', function (done) {
            let progressCalled, nResult = 0;
            let sql='select * from customer limit 5; select * from seller limit 10; ';
            if (driver==='sqlServer'){
                sql='select top 5 * from customer; select top 10 * from seller; ';
            }
            sqlConn.queryBatch(sql)
            .progress(function (result) {
                expect(result).toBeDefined();
                expect(result.length).toBe(5);
                nResult += 1;
                progressCalled = true;
            })
            .fail(function (err) {
                expect(err).toBeUndefined();
                done();
            })
            .done(function (result) {
                expect(result.length).toBe(10);
                expect(nResult).toBe(1);
                expect(progressCalled).toBeTruthy();
                done();
            });
        });


        it('notify should be called from queryRaw when multiple result got (three select)', function (done) {
            var len            = [];
            let sql='select * from seller limit 1;select * from seller limit 3;select * from customer limit 5;'+
                'select * from seller limit 10;select * from customer limit 2;';
            if (driver==='sqlServer'){
                sql='select top 1 * from seller;select top 3 *  from seller;select top 5  * from customer;'+
                    'select top 10 * from seller;select top 2 * from customer;';
            }

            sqlConn.queryBatch(sql)
            .progress(function (result) {
                len.push(result.length);
                return true;
            })
            .fail(function (err) {
                expect(err).toBeUndefined();
                done();
            })
            .done(function (result) {
                len.push(result.length);
                expect(len).toEqual([1,3, 5, 10, 2]);
                done();
            });
        });
    });


    describe('querylines', function () {


        it('queryLines should return as many meta as read tables ', function (done) {
            console.log("queryLines check start");
            var nResp = 0;
            let sql='select * from customer limit 10; select * from seller limit 20;select * from customerkind limit 2';
            if (driver==='sqlServer') {
                sql='select top 10 * from customer ; select top 20 * from seller ;select top 2 * from customerkind';
            }

            sqlConn.queryLines(sql,true)
            .progress(function (r) {
                expect(r).toBeDefined();
                if (r.meta) {
                    nResp += 1;
                }
            })
            .done(function () {
                expect(nResp).toBe(3);
                done();
            })
            .fail(function (err) {
                expect(err).toBeUndefined();
                done();
            });

        });

        it('meta returned from queryLines should be arrays ', function (done) {
            let sql= 'select * from sellerkind limit 10; select * from seller limit 20; select * from customerkind limit 2;';
            if (driver==='sqlServer') {
                sql= 'select top 10 * from sellerkind ; select top 20 * from seller ; select top 2 * from customerkind;';
            }
            sqlConn.queryLines(sql, true)
            .progress(function (r) {
                if (r.meta) {
                    expect(r.meta).toEqual(jasmine.any(Array));
                }
            })
            .done(function () {
                done();
            })
            .fail(function (err) {
                expect(err).toBeUndefined();
                done();
            });

        });

        it('queryLines should return all rows one at a time', function (done) {
            var nResp = 0;
            let sql='select * from seller limit 5;';
            if (driver==='sqlServer') {
                sql= 'select top 5 * from seller ;';
            }
            sqlConn.queryLines(sql, true)
            .progress(function (r) {
                expect(r).toBeDefined();
                if (r.row) {
                    nResp += 1;
                }
            })
            .done(function () {
                expect(nResp).toBe(5);
                done();
            })
            .fail(function (err) {
                expect(err).toBeUndefined();
                done();
            });

        });

        it('queryLines should return row as arrays ', function (done) {
            var nResp = 0;
            let sql='select * from customerkind limit 5;';
            if (driver==='sqlServer') {
                sql= 'select top 5 * from customerkind ;';
            }
            sqlConn.queryLines(sql, true)
            .progress(function (r) {
                if (r.row) {
                    nResp += 1;
                    expect(r.row).toEqual(jasmine.any(Array));
                }
            })
            .done(function () {
                expect(nResp).toBe(5);
                done();
            })
            .fail(function (err) {
                expect(err).toBeUndefined();
                done();
            });
        });

        it('queryLines should return row as objects when raw=false ', function (done) {
            var nResp = 0;
            var sql= 'select  * from customerkind limit 5;';
            if (driver==='sqlServer'){
                sql= 'select  top 5 * from customerkind;';
            }
            sqlConn.queryLines(sql, false)
            .progress(function (r) {
                if (r.row) {
                    nResp += 1;
                    expect(r.row).toEqual(jasmine.any(Object));
                    //noinspection JSUnresolvedVariable
                    expect(r.row.idcustomerkind).toEqual(jasmine.any(Number));
                    //noinspection JSUnresolvedVariable
                    expect(r.row.name).toEqual(jasmine.any(String));
                }
            })
            .done(function () {
                expect(nResp).toBe(5);
                done();
            })
            .fail(function (err) {
                expect(err).toBeUndefined();
                done();
            });
        });

        it('queryLines should work with multiple results ', function (done) {
            var nResp = 0;
            var sql= 'select  * from customerkind limit 5; select * from customer limit 10;';
            if (driver==='sqlServer'){
                sql= 'select  top 5 * from customerkind; select top 10 * from customer;';
            }

            sqlConn.queryLines(sql, false)
            .progress(function (r) {
                if (r.row) {
                    nResp += 1;
                    expect(r.row).toEqual(jasmine.any(Object));
                    if (nResp <= 5) {
                        //noinspection JSUnresolvedVariable
                        expect(r.row.idcustomerkind).toEqual(jasmine.any(Number));
                        //noinspection JSUnresolvedVariable
                        expect(r.row.name).toEqual(jasmine.any(String));
                        //noinspection JSUnresolvedVariable
                        expect(r.row.surname).toBeUndefined();
                    }
                    else {
                        //noinspection JSUnresolvedVariable
                        expect(r.row.idcustomer).toEqual(jasmine.any(Number));
                        //noinspection JSUnresolvedVariable
                        expect(r.row.surname).toEqual(jasmine.any(String));
                        //noinspection JSUnresolvedVariable
                        expect(r.row.rnd).toBeUndefined();
                    }
                }
            })
            .done(function () {
                expect(nResp).toBe(15);
                done();
            })
            .fail(function (err) {
                expect(err).toBeUndefined();
                done();
            });
        });
    });

    describe('clear dataBase', function () {
        it('should run the destroy script', function (done) {
            sqlConn.run(fs.readFileSync(path.join('test',driver+'Destroy.sql')).toString())
            .done(function () {
                expect(true).toBeTruthy();
                done();
            })
            .fail(function (res) {
                expect(res).toBeUndefined();
                done();
            });
        }, 30000);
    });
});