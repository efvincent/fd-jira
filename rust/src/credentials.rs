/// Credentials for Jira to call the API. For example, the un/pw of a service account.
/// or if running interactively, the current user
#[derive(Debug)]
pub struct Creds {
    pub un: String,
    pub pw: String,
}

const ENV_KEY: &'static str = "JIRA_CREDS";

/// Returns Jira credentials retrieved from an environment variable
pub fn get_creds() -> Result<Creds, String> {
    match std::env::var_os(ENV_KEY) {
        Some(val) => {
            match val.as_os_str().to_str() {
                Some(val) => {
                    let unpw: Vec<&str> = val.rsplit(':').collect();
                    if unpw.len() != 2 {
                        Err(format!(
                            "Environment key {} should have the format 'un:pw'",
                            ENV_KEY
                        ))
                    } else {
                        // Note the rsplit function returns the parts in reverse order
                        Ok(Creds {
                            un: unpw[1].to_string(),
                            pw: unpw[0].to_string(),
                        })
                    }
                }
                None => Err(format!(
                    "Environment key {} has invalid credentials value. Use the format 'un:pw'",
                    ENV_KEY
                )),
            }
        }
        None => Err(format!("Environment key {} not found", ENV_KEY)),
    }
}
