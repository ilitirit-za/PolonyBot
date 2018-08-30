BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS `Tournament` (
	`Id`	TEXT,
	`Name`	TEXT,
	`Url`	TEXT,
	`Game`	TEXT,
	`Status`	TEXT,
	`PlannedStartDate`	TEXT,
	`StartedAt`	TEXT,
	`RegisteredBy`	TEXT,
	`RegisteredAt`	TEXT,
	`UpdatedAt`	TEXT,
	PRIMARY KEY(`Id`)
);
CREATE TABLE IF NOT EXISTS `Participant` (
	`Id`	TEXT,
	`TournamentId`	TEXT,
	`Name`	TEXT,
	PRIMARY KEY(`Id`,`TournamentId`)
);
COMMIT;
