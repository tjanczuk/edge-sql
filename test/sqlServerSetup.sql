SET NOCOUNT ON

CREATE TABLE customer(
	idcustomer int NOT NULL,
	name varchar(100) NULL,
	age int NULL,
	birth datetime NULL,
	surname varchar(100) NULL,
	stamp datetime NULL,
	random int NULL,
	curr decimal(19,2) NULL,
    CONSTRAINT xpkcustomer PRIMARY KEY  (idcustomer)
) ;
GO



CREATE PROCEDURE ctemp AS 
BEGIN
DECLARE @i int;
SET @i = 1;
while @i < 500 BEGIN
 insert into customer(idcustomer,name,age,birth,surname,stamp,random,curr) values(
			 @i, 		 concat('name',convert(VARCHAR(10),@i) ),
			10+@i,		'2010-24-09 12:27:38',
			concat('surname_',convert(VARCHAR(10),@i*2+100000)),
			GETDATE(),
			RAND()*1000,
			RAND()*10000 );
 SET @i = @i+1;
END 
END


GO

exec ctemp;
GO

DROP PROCEDURE  ctemp;





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
	CONSTRAINT PK_seller PRIMARY KEY  (idseller)
);

GO

CREATE PROCEDURE ctemp AS
BEGIN
declare @i int
set @i=1;
while (@i<60) BEGIN
insert into seller (idseller,name,age,birth,surname,stamp,random,curr,cf) values(
			 @i,
			 concat('name',convert(varchar(10),@i)	)	,10+@i,
			'2010-24-02 12:27:38',
			concat('surname_',convert(varchar(10),@i*2+100000)),
			GETDATE(),
			RAND()*1000,
			RAND()*10000,
			convert(varchar(20),RAND()*100000)
            );
set @i=@i+1;
end 

END

GO

exec ctemp;


DROP PROCEDURE  ctemp;





CREATE TABLE sellerkind(
	idsellerkind int NOT NULL,
	name varchar(100) NULL,
	rnd int NULL,
    CONSTRAINT PK_sellerkind PRIMARY KEY  (idsellerkind)
);



GO

CREATE PROCEDURE ctemp AS 
BEGIN
declare @i int

set @i=0;
while (@i<20) BEGIN
insert into sellerkind (idsellerkind,name,rnd) values(
			 @i*30,
			 concat('name',convert(varchar(10),@i*30)),
			 RAND()*1000
		);
set @i=@i+1;
end 

END


GO

exec ctemp;


DROP PROCEDURE  ctemp;



CREATE TABLE customerkind(
	idcustomerkind int NOT NULL,
	name varchar(100) NULL,
	rnd int NULL,
    CONSTRAINT PK_customerkind PRIMARY KEY  (idcustomerkind)
) ;



GO

CREATE PROCEDURE ctemp AS
BEGIN
declare @i int

set @i=0;
while (@i<40) BEGIN
insert into customerkind (idcustomerkind,name,rnd) values(
			 @i*3,
			 concat('name',convert(varchar(10),@i*3)),
			RAND()*1000
		);
set @i=@i+1;
end ;


END


GO

exec ctemp;


DROP PROCEDURE  ctemp;

GO

CREATE PROCEDURE testSP2 (@esercizio int,   @meseinizio int,  @mess varchar(200),   @defparam decimal(19,2)=2 ) AS
BEGIN         
         select 'aa' as colA, 'bb' as colB, 12 as colC , @esercizio as original_esercizio,
         replace(@mess,'a','z') as newmess,   @defparam*2 as newparam;
END
GO


CREATE PROCEDURE testSP1( @esercizio int, @meseinizio int, @mesefine int OUTPUT,	@mess varchar(200), 	@defparam decimal(19,2)=2 ) AS
BEGIN
	set @mesefine= 12;
	select 'a' as colA, 'b' as colB, 12 as colC , @esercizio as original_esercizio,
		replace(@mess,'a','z') as newmess,
		@defparam*2 as newparam;
END

GO


CREATE  PROCEDURE  testSP3 (@esercizio int=0) AS
BEGIN
	select top 100 * from customer ;
	select top 100 * from seller ;
	select top 10 * from customerkind as c2 ;
	select top 10 * from sellerkind as s2 ;
END

GO
