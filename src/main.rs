#![allow(non_snake_case)] // b/c serialized message types from Jira use camelCase
#![allow(dead_code)]
mod jira_types;
mod credentials;
mod jira_api;
mod jira_sqlite;

use credentials::*;
use structopt::StructOpt;

static JIRA_URL: &str = "https://jira.walmart.com/rest/api/2";

#[derive(StructOpt, Debug)]
#[structopt(name = "cli_args")]
struct Opt {
    /// Synchronize issues with changes that occured since last sync
    sync:bool
}

fn main() {
    let opt = Opt::from_args();
    jira_sqlite::init_database().unwrap();
    if opt.sync {
        jira_sqlite::write_issues().unwrap();
    }
    let creds = get_creds().unwrap();
    jira_api::get_issue_snapshot(&creds, JIRA_URL, "RCTFD-4472");
}