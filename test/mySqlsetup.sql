

drop table IF EXISTS customer;


CREATE TABLE customer(
	idcustomer int NOT NULL,
	name varchar(100) NULL,
	age int NULL,
	birth datetime NULL,
	surname varchar(100) NULL,
	stamp datetime NULL,
	random int NULL,
	curr decimal(19,2) NULL,
    KEY PK_customer (idcustomer)
) ;
GO

DROP PROCEDURE if exists ctemp;
GO


CREATE PROCEDURE ctemp ()
BEGIN
SET @i = 1;
while @i < 500 DO
 insert into customer(idcustomer,name,age,birth,surname,stamp,random,curr) values(
			 @i, 		 concat('name',convert(@i,CHAR(10)) ),
			10+@i,		'2010-09-24 12:27:38',
			concat('surname_',convert(@i*2+100000, CHAR(10))),
			NOW(),
			RAND()*1000,
			RAND()*10000 );
 SET @i = @i+1;
END WHILE;
END


GO

call ctemp;
GO

DROP PROCEDURE if exists ctemp;



drop table IF EXISTS seller;


CREATE TABLE seller(
	idseller int NOT NULL,
	name varchar(100) NULL,
	age int NULL,
	birth datetime NULL,
	surname varchar(100) NULL,
	stamp datetime NULL,
	random int NULL,
	curr decimal(19,2) NULL,
	cf varchar(200),
	KEY PK_seller (idseller)
);

GO

CREATE PROCEDURE ctemp ()
BEGIN
set @i=1;
while (@i<60) DO
insert into seller (idseller,name,age,birth,surname,stamp,random,curr,cf) values(
			 @i,
			 concat('name',convert(@i,char(10))	)	,10+@i,
			'2010-09-24 12:27:38',
			concat('surname_',convert(@i*2+100000,char(10))),
			NOW(),
			RAND()*1000,
			RAND()*10000,
			convert(RAND()*100000,char(20))
            );
set @i=@i+1;
end while;

END
GO

call ctemp;


DROP PROCEDURE if exists ctemp;


drop table IF EXISTS sellerkind;


CREATE TABLE sellerkind(
	idsellerkind int NOT NULL,
	name varchar(100) NULL,
	rnd int NULL,
    KEY PK_sellerkind (idsellerkind)
);



GO

CREATE PROCEDURE ctemp ()
BEGIN
set @i=0;
while (@i<20) DO
insert into sellerkind (idsellerkind,name,rnd) values(
			 @i*30,
			 concat('name',convert(@i*30,char(10))),
			 RAND()*1000
		);
set @i=@i+1;
end while;

END


GO

call ctemp;


DROP PROCEDURE if exists ctemp;


drop table IF EXISTS customerkind;

CREATE TABLE customerkind(
	idcustomerkind int NOT NULL,
	name varchar(100) NULL,
	rnd int NULL,
     KEY PK_customerkind (idcustomerkind)
) ;


DROP PROCEDURE if exists ctemp;

GO

CREATE PROCEDURE ctemp ()
BEGIN
set @i=0;
while (@i<40) DO
insert into customerkind (idcustomerkind,name,rnd) values(
			 @i*3,
			 concat('name',convert(@i*3,char(10))),
			RAND()*1000
		);
set @i=@i+1;
end while;


END


GO

call ctemp;


DROP PROCEDURE if exists ctemp;


DROP PROCEDURE IF EXISTS testSP2;

GO


CREATE PROCEDURE testSP2 (IN esercizio int,   IN meseinizio int,   IN mess varchar(200),   IN defparam decimal(19,2) )
BEGIN
         if (defparam is null) THEN set defparam=2; 		 END IF;
         select 'aa' as colA, 'bb' as colB, 12 as colC , esercizio as original_esercizio,
         replace(mess,'a','z') as newmess,   defparam*2 as newparam;
END
GO

DROP PROCEDURE IF EXISTS testSP1;

GO

CREATE PROCEDURE testSP1( esercizio int, meseinizio int, out mesefine int ,	mess varchar(200), 	defparam decimal(19,2) )
BEGIN
	if (defparam is null) THEN set defparam=2; 		 END IF;
	set mesefine= 12;
	select 'a' as colA, 'b' as colB, 12 as colC , esercizio as original_esercizio,
		replace(mess,'a','z') as newmess,
		defparam*2 as newparam;
END

GO

DROP PROCEDURE IF EXISTS testSP3;

GO

CREATE  PROCEDURE  testSP3 (esercizio int)
BEGIN
    IF (ESERCIZIO IS NULL) then set ESERCIZIO=0; end IF;
	select * from customer limit 100;
	select * from seller limit 100;
	select * from customerkind as c2 limit 10;
	select * from sellerkind as s2 limit 10;
END

GO
