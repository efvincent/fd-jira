use crate::credentials::Creds;
use crate::jira_types;
use chrono::prelude::{DateTime, FixedOffset, Utc};
use curl::easy::{Auth, Easy2, Handler, WriteError};
use serde::{Deserialize, Serialize};

/// Jira returns a structure with only fields within the issue struct. Not sure why /shrug
#[derive(Serialize, Deserialize, Debug)]
pub struct JiraFields {
    summary: String,
    resolutiondate: DateTime<Utc>,
    created: DateTime<Utc>,
    description: String,
    updated: DateTime<Utc>,
}

/// The shape of the issue when it's a search result is different. Also not sure we need
/// to do it this way.
#[derive(Serialize, Deserialize, Debug)]
pub struct IssueSearchResult {
    pub id: String,
    pub key: String,
}

/// The set of search results coming from Jira. Includes the results + info about
/// which slice this is and what the total number of hits is
#[derive(Serialize, Deserialize, Debug)]
pub struct IssueSearchResultSet {
    pub startAt: usize,
    pub maxResults: usize,
    pub total: usize,
    pub err: Option<String>,
    pub issues: Vec<IssueSearchResult>,
}

/// Supports the curl crate by providing a type that can have a handler trait. The handler
/// trait is similar in function to an interface. It has a `write` method, and it's passed
/// to the `Easy2` constructor and used to collect http responses
struct Collector {
    content: String,
}

impl Collector {
    fn new() -> Collector {
        Collector {
            content: String::new(),
        }
    }
}

impl Handler for Collector {
    fn write(&mut self, data: &[u8]) -> Result<usize, WriteError> {
        self.content.push_str(std::str::from_utf8(data).unwrap());
        Ok(data.len())
    }
}

/// Makes an http request to `url` with credentials, and passes the collected
/// `String` to the passed function `f`. This approach allows `f` to borrow
/// the content without an additional allocation
fn curl_call_do<T>(creds: &Creds, url: &str, f: impl Fn(&str) -> T) -> T {
    let collector = Collector::new();
    let mut http_client = Easy2::new(collector);
    http_client.url(&url).unwrap();
    http_client.http_auth(Auth::new().basic(true)).unwrap();
    http_client.username(&creds.un).unwrap();
    http_client.password(&creds.pw).unwrap();
    http_client.perform().unwrap();
    let collector = http_client.get_ref();
    f(&collector.content)
}

/// Prepares a synchronization query for the Jira API given the `project` and
/// the `updatedSince` date.
fn make_query(project: &str, updatedSince: &DateTime<FixedOffset>) -> String {
    let dt = updatedSince.format("%Y-%m-%d %H:%M");
    let query = format!("project={} AND updatedDate >= \"{}\"", project, dt);
    urlencoding::encode(&query)
}

/// Returns an `IssueSearchResultSet` given the `base_url` of the Jira installation, credentials,
/// and the position in the total set of results to begin returning elements. For example if
/// `startAt` is 25 and there are 200 results, records number 25-75 will be returned because
/// the default number of records to return is 50.
pub fn get_changed_issues_do(
    creds: &Creds,
    base_url: &str,
    startAt: usize,
) -> IssueSearchResultSet {
    let query = make_query(
        "RCTFD",
        &DateTime::parse_from_rfc3339("2019-09-01T00:00:00-05:00").unwrap(),
    );
    let url = format!(
        "{}/search?jql={}&expand=names&maxResults=100&fields=updated&startAt={}",
        base_url, query, startAt
    );
    let f = |raw: &str| {
        let mut sr: IssueSearchResultSet = match serde_json::from_str(&raw) {
            Ok(result_set) => result_set,
            Err(err) => IssueSearchResultSet {
                startAt: 0,
                maxResults: 0,
                err: Some(err.to_string()),
                total: 0,
                issues: Vec::new(),
            },
        };
        sr.startAt = startAt;
        sr
    };
    curl_call_do(creds, &url, f)
}

pub fn get_changed_issues(creds: &Creds, base_url: &str, startAt: usize) -> IssueSearchResultSet {
    let query = make_query(
        "RCTFD",
        &DateTime::parse_from_rfc3339("2019-08-01T00:00:00-05:00").unwrap(),
    );
    let url = format!(
        "{}/search?jql={}&expand=names&maxResults=100&fields=updated&startAt={}",
        base_url, query, startAt
    );
    let f = |raw: &str| {
        let mut sr: IssueSearchResultSet = match serde_json::from_str(&raw) {
            Ok(result_set) => result_set,
            Err(err) => IssueSearchResultSet {
                startAt: 0,
                maxResults: 0,
                err: Some(err.to_string()),
                total: 0,
                issues: Vec::new(),
            },
        };
        sr.startAt = startAt;
        sr
    };
    curl_call_do(creds, &url, f)
}

pub fn get_issue_snapshot(creds: &Creds, base_url: &str, issue: &str) {
    let url = format!("{}/issue/{}?fields=assignee,status,summary,description,created,updated,resolutiondate,issuetype,components,priority,resolution,customfield_10002", 
        base_url, issue);
    // let issue: Issue = serde_json::from_str(raw).unwrap();
    // return issue
    let f = |raw: &str| {
        let issue = jira_types::Issue::from_value(&raw);
        println!("issue: {:#?}", issue);
    };
    curl_call_do(creds, &url, f);
}
