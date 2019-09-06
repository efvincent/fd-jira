
#[derive(Debug)]
/// Credentials for Jira to call the API. For example, the un/pw of a service account.
/// or if running interactively, the current user
struct Creds {
    un: String,
    pw: String
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

use curl::easy::{Auth,Easy};

fn curl_call(un:&str, pw:&str, url:&str) {
    let mut easy = Easy::new();
    easy.url(url).unwrap();
    easy.http_auth(Auth::new().basic(true)).unwrap();
    easy.username(un).unwrap();
    easy.password(pw).unwrap();
    println!("easy:\n{:?}\nun: {}\npw: {}\n", easy, un, pw);
    
    easy.write_function(|data| {
        println!("{:?}", std::str::from_utf8(data));
        Ok(data.len())
    }).unwrap();
    easy.perform().unwrap();
    println!("{}", easy.response_code().unwrap());
}

fn main() {
    let creds = get_creds();
    match creds {
        Ok(c) => {
            curl_call(c.un.as_str(), c.pw.as_str(), "https://jira.walmart.com/rest/api/2/issue/RCTFD-4223?fields=assignee,status,summary,description,created,updated,resolutiondate,issuetype,components,priority,resolution");
        },
        Err(e) => println!("Error: {}",e)
    };
}
