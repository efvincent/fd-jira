#![allow(non_snake_case)] // b/c serialized message types from Jira use camelCase
#![allow(dead_code)]
mod jira_types;

use curl::easy::{Auth,Easy2,Handler,WriteError};
use serde::{Serialize,Deserialize};
use chrono::prelude::*;
use rusqlite::{Connection, params, NO_PARAMS};
use structopt::StructOpt;

/// Credentials for Jira to call the API. For example, the un/pw of a service account.
/// or if running interactively, the current user
#[derive(Debug)]
struct Creds {
    un: String,
    pw: String
}

/// Jira returns a structure with only fields within the issue struct. Not sure why /shrug
#[derive(Serialize,Deserialize,Debug)]
struct JiraFields {
    summary: String,
    resolutiondate: DateTime<Utc>,
    created: DateTime<Utc>,
    description: String,
    updated: DateTime<Utc>
}

/// Top level Issue. Will probably change the way these are deserialized to make it less
/// of a literal translation of what comes from Jira into something that makes more
/// sense for this application
#[derive(Serialize,Deserialize,Debug)]
struct Issue {
    id: String,
    key: String,
    fields: JiraFields
}

/// The shape of the issue when it's a search result is different. Also not sure we need
/// to do it this way.
#[derive(Serialize,Deserialize,Debug)]
struct IssueSearchResult {
    id: String,
    key: String
}

/// The set of search results coming from Jira. Includes the results + info about
/// which slice this is and what the total number of hits is
#[derive(Serialize,Deserialize,Debug)]
struct IssueSearchResultSet {
    startAt: usize,
    maxResults: usize,
    total: usize,
    err: Option<String>,
    issues: Vec<IssueSearchResult>
}

static JIRA_URL: &str = "https://jira.walmart.com/rest/api/2";

/// Returns Jira credentials retrieved from an environment variable
fn get_creds() -> Result<Creds, String> {
    let key: &'static str = "JIRA_CREDS";
    match std::env::var_os(key) {
        Some (val) => {
            match val.as_os_str().to_str() {
                Some (val) => {
                    let unpw: Vec<&str> = val.rsplit(':').collect();
                    if unpw.len() != 2 {
                        Err(format!("Environment key {} should have the format 'un:pw'", key))
                    } else {
                        // Note the rsplit function returns the parts in reverse order
                        Ok(Creds { un: unpw[1].to_string(), pw: unpw[0].to_string() })
                    }
                },
                None => Err(format!("Environment key {} has invalid credentials value. Use the format 'un:pw'", key))
            }
        },
        None => 
            Err(format!("Environment key {} not found", key))
    }
}

/// Supports the curl crate by providing a type that can have a handler trait. The handler
/// trait is similar in function to an interface. It has a `write` method, and it's passed
/// to the `Easy2` constructor and used to collect http responses
struct Collector {
    content: String
}

impl Collector {
    fn new() -> Collector {
        Collector { content: String::new() }
    }
}

impl Handler for Collector {
    fn write(&mut self, data: &[u8]) -> Result<usize, WriteError> {
        self.content.push_str(std::str::from_utf8(data).unwrap());
        Ok(data.len())
    }
}

fn curl_call(creds:&Creds, url:String) -> String  {
    let mut http_client = Easy2::new(Collector::new());
    http_client.url(&url).unwrap();
    http_client.http_auth(Auth::new().basic(true)).unwrap();
    http_client.username(&creds.un).unwrap();
    http_client.password(&creds.pw).unwrap();
    http_client.perform().unwrap();
    let collector = http_client.get_ref();
    return collector.content.clone();
}

fn make_query(project:&str, updatedSince:&DateTime<FixedOffset>) -> String {
    let dt = updatedSince.format("%Y-%m-%d %H:%M");
    let query = format!("project={} AND updatedDate >= \"{}\"", project, dt);
    urlencoding::encode(&query)
}

fn get_changed_issues(creds:&Creds, base_url: String, startAt:usize) -> IssueSearchResultSet {
    let query = make_query("RCTFD", &DateTime::parse_from_rfc3339("2019-08-01T00:00:00-05:00").unwrap());
    let url = format!("{}/search?jql={}&expand=names&maxResults=100&fields=updated&startAt={}", base_url, query, startAt);
    let raw = curl_call(creds, url);
    let mut sr: IssueSearchResultSet = 
        match serde_json::from_str(raw.as_str()) {
            Ok(result_set) => result_set,
            Err(err) => IssueSearchResultSet {
                    startAt: 0,
                    maxResults: 0,
                    err: Some(err.to_string()),
                    total: 0,
                    issues: Vec::new()
                }
        };
    sr.startAt = startAt;
    return sr;
}

fn get_issue_snapshot(creds:&Creds, base_url: String, issue:String) -> Issue {
    let url = format!("{}/issue/{}?fields=assignee,status,summary,description,created,updated,resolutiondate,issuetype,components,priority,resolution", 
        base_url, issue);
    let raw = curl_call(creds, url);
    let issue: Issue = serde_json::from_str(raw.as_str()).unwrap();
    return issue
}

fn do_gira() {
    let creds = get_creds();
    match creds {
        Ok(c) => {
            //let issue = get_issue_snapshot(&c, base_url.clone(), "RCTFD-4223".to_string());
            let sr = get_changed_issues(&c, JIRA_URL.to_string(), 0);
            match sr.err {
                None =>
                    (),
                Some(err) =>
                    println!("Error: {}", err)
            }
        },
        Err(e) => println!("Error: {}",e)
    };
}

#[derive(StructOpt, Debug)]
#[structopt(name = "cli_args")]
struct Opt {
    /// Synchronize issues with changes that occured since last sync
    sync:bool
}

fn main() {
    let opt = Opt::from_args();
    init_database().unwrap();
    if opt.sync {
        write_issues().unwrap();
    }
}

#[derive(Debug)]
struct Person {
    id: i32,
    name: String,
    data: Option<Vec<u8>>,
}

fn write_issues() -> Result<(), rusqlite::Error> {
    // get the issues
    let creds = get_creds().unwrap();
    let conn = Connection::open("./fd-jira.db").unwrap();
    let mut cur_start:usize = 0;
    let mut count:usize = 0;
    
    loop {
        let issues = get_changed_issues(&creds, JIRA_URL.to_string(), cur_start);
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
fn init_database() -> Result<(), rusqlite::Error> {
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

fn do_sqlite() -> rusqlite::Result<()> {
    let conn = Connection::open_in_memory()?;

    conn.execute(
        "CREATE TABLE person (
                  id              INTEGER PRIMARY KEY,
                  name            TEXT NOT NULL,
                  data            BLOB
                  )",
        params![],
    )?;
    let me = Person {
        id: 0,
        name: "Steven".to_string(),
        data: None,
    };
    conn.execute(
        "INSERT INTO person (name, data)
                  VALUES (?1, ?2)",
        params![me.name, me.data],
    )?;

    let mut stmt = conn.prepare("SELECT id, name, data FROM person")?;
    let person_iter = stmt.query_map(params![], |row| {
        Ok(Person {
            id: row.get(0)?,
            name: row.get(1)?,
            data: row.get(2)?,
        })
    })?;

    for person in person_iter {
        println!("Found person {:?}", person.unwrap());
    }
    Ok(())
}