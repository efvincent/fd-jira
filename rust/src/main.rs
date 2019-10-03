#![allow(non_snake_case)] // b/c serialized message types from Jira use camelCase
#![allow(dead_code)]
mod credentials;
mod jira_api;
mod jira_sqlite;
mod jira_types;

use credentials::*;
use structopt::StructOpt;
// use crossterm::{
//     execute, input, style, AsyncReader, Clear, ClearType, Color, Crossterm, Goto, InputEvent,
//     KeyEvent, PrintStyledFont, RawScreen, Result, Show,
// };

static JIRA_URL: &str = "https://jira.walmart.com/rest/api/2";

#[derive(StructOpt, Debug)]
#[structopt(name = "fd-jira")]
struct Opt {
    /// Synchronize issues with changes that occured since last sync
    sync: bool,
}

fn sync_issues() {
    // get the first (and possibly only) batch of issues
    let conn = rusqlite::Connection::open("./fd-jira.db").unwrap();
    let mut cur_start: usize = 0;
    let mut count: usize = 0;
    let creds = get_creds().unwrap();
    loop {
        let query = crate::jira_api::get_changed_issues(&creds, JIRA_URL, cur_start);
        jira_sqlite::write_issues(&conn, &query.issues).unwrap();
        count += query.issues.len();
        if count < query.total {
            cur_start = count;
        } else {
            println!("sync complete. {} issues refreshed.", count);
            break;
        }
    }
}

fn main() -> Result<()> {
    // let crossterm = Crossterm::new();
    // let _raw = RawScreen::into_raw_mode();
    // crossterm.cursor().hide()?;

    let opt = Opt::from_args();
    jira_sqlite::init_database().unwrap();
    if opt.sync {
        sync_issues();
    }
    let creds = get_creds().unwrap();
    jira_api::get_issue_snapshot(&creds, JIRA_URL, "RCTFD-4472");
    
    Ok(())
}
