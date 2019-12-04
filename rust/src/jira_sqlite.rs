use crate::jira_api::IssueSearchResult;
use rusqlite::{params, Connection, NO_PARAMS};

pub fn write_issues(
    conn: &Connection,
    issues: &[IssueSearchResult],
) -> Result<(), rusqlite::Error> {
    for issue in issues {
        conn.execute(
            " insert into issue (id, [key], last_updated) values (?1, ?2, ?3)
            on conflict(id) do nothing",
            params![issue.id, issue.key, 100],
        )
        .unwrap();
    }
    Ok(())
}

/// Create the Sqlite database file if it doesn't exist
pub fn init_database() -> Result<(), rusqlite::Error> {
    let conn = Connection::open("./fd-jira.db")?;
    conn.execute(
        "create table if not exists issue (
            id integer primary key,
            [key] varchar(50) not null unique,
            last_updated integer null
            )",
        NO_PARAMS,
    )?;

    conn.execute(
        "create table if not exists project_sync (
            project varchar(20) primary key,
            last_snapshot varchar(50) null
        )
        ",
        NO_PARAMS,
    )?;

    conn.close().unwrap();
    Ok(())
}
