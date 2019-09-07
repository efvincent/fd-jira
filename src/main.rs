#![allow(non_snake_case)] // b/c serialized message types from Jira use camelCase
#![allow(dead_code)]

use curl::easy::{Auth,Easy2,Handler,WriteError};
use serde::{Serialize,Deserialize};
use chrono::prelude::*;

#[derive(Debug)]
/// Credentials for Jira to call the API. For example, the un/pw of a service account.
/// or if running interactively, the current user
struct Creds {
    un: String,
    pw: String
}

#[derive(Serialize,Deserialize,Debug)]
struct JiraFields {
    summary: String,
    resolutiondate: DateTime<Utc>,
    created: DateTime<Utc>,
    description: String,
    updated: DateTime<Utc>
}

#[derive(Serialize,Deserialize,Debug)]
struct Issue {
    id: String,
    key: String,
    fields: JiraFields
}

#[derive(Serialize,Deserialize,Debug)]
struct IssueSearchResult {
    id: String,
    key: String
}

#[derive(Serialize,Deserialize,Debug)]
struct IssueSearchResultSet {
    startAt: i32,
    maxResults: i32,
    total: i32,
    err: Option<String>,
    issues: Vec<IssueSearchResult>
}

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
    println!("{}", query);
    urlencoding::encode(&query)
}

fn get_changed_issues(creds:&Creds, base_url: String, startAt:i32) -> IssueSearchResultSet {
    let query = make_query("RCTFD", &DateTime::parse_from_rfc3339("2019-09-06T13:30:00-05:00").unwrap());
    let url = format!("{}/search?jql={}&expand=names&fields=updated&startAt={}", base_url, query, startAt);
    println!("{}", url);
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

fn main() {
    let creds = get_creds();
    let base_url = "https://jira.walmart.com/rest/api/2".to_string();
    match creds {
        Ok(c) => {
            //let issue = get_issue_snapshot(&c, base_url.clone(), "RCTFD-4223".to_string());
            //println!("{:?}", issue);
            let sr = get_changed_issues(&c, base_url, 0);
            match sr.err {
                None =>
                    println!("{:?}", sr),
                Some(err) =>
                    println!("Error: {}", err)
            }
        },
        Err(e) => println!("Error: {}",e)
    };
}
