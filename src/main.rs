
#[derive(Debug)]
struct Creds {
    un: String,
    pw: String
}

fn get_creds() -> Result<Creds,String> {
    const KEY:&str = "JIRA_CREDS";
    match std::env::var_os(KEY) {
        Some (val) => 
            Ok(Creds { un: format!("{:?}",val), pw: "test".to_string() }),
        None => 
            Err(format!("Environment key {} not found", KEY))
    }
}

fn main() {
    println!("{:?}", get_creds());
}
