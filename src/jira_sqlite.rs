use rusqlite::{Connection, params, NO_PARAMS};
use crate::credentials::*;

pub fn write_issues(jira_url:&str) -> Result<(), rusqlite::Error> {
    // get the issues
    let creds = get_creds().unwrap();
    let conn = Connection::open("./fd-jira.db").unwrap();
    let mut cur_start:usize = 0;
    let mut count:usize = 0;
    
    loop {
        let issues = crate::jira_api::get_changed_issues(&creds, jira_url, cur_start);
        let cur_count = issues.issues.len();
        for issue in issues.issues {
            conn.execute(
                " insert into issue (id, [key], last_updated) values (?1, ?2, ?3)
                on conflict(id) do nothing", 
                params![issue.id, issue.key, 100]).unwrap();
        }
        count = count + cur_count;
        println!("processed {} out of {}", count, issues.total);
        if count < issues.total {
            cur_start = count;
            println!("continuing with startAt = {}", cur_start)
        } else {
            println!("processing complete with count {} of {}", count, issues.total);
            break;
        }
    }

    conn.close().unwrap();
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
        NO_PARAMS)?;

    conn.execute(
        "create table if not exists project_sync (
            project varchar(20) primary key,
            last_snapshot varchar(50) null
        )
        ",
        NO_PARAMS)?;

    conn.close().unwrap();
    Ok(())
}