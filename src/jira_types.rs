use chrono::prelude::*;
use serde_json::Value;

#[derive(Debug)]
pub enum IssueType {
  Unknown,
  Issue,
  Story,
  Epic,
  Other(String)
}

impl IssueType {
  pub fn from_string(s:&str) -> IssueType {
    match s {
      "Issue" => IssueType::Issue,
      "Story" => IssueType::Story,
      "Epic" => IssueType::Epic,
      unk => IssueType::Other(String::from(unk))
    }
  }
}

#[derive(Debug)]
pub enum Component {
  Unknown,
  Mojo,
  Phoenix,
  Wolverine,
  Ironman,
  Product,
  Design,
  Other(String)
}

#[derive(Debug)]
pub enum Status {
  Unknown,
  Backlog,
  ReadyForWork,
  Active,
  Done,
  Deleted,
  Other(String)
}

impl Status {
    pub fn from_string(s:&str) -> Status {
        match s {
            "Done" => Status::Done,
            "Deleted" => Status::Deleted,
            unk => Status::Other(String::from(unk))
        }
    }
}

#[derive(Debug)]
pub struct Person {
  pub key: String,
  pub email: String,
  pub name: String
}

impl Person {
    pub fn from_value_opt(v:&Value) -> Option<Person> {
        println!("person? {:?}", v);
        let key:&str = &vstr(&v["key"]);
        let email:&str = &vstr(&v["emailAddress"]);
        let name:&str = &vstr(&v["displayName"]);
        match (key,email,name) {
            ("",_,_) => None,
            (k,em,nm) => Some(Person {
                key: String::from(k),
                email: String::from(em),
                name: String::from(nm)
            })
        }
    }
}

#[derive(Debug)]
pub struct Issue {
    pub key: String,
    pub id: i64,
    pub summary: String,
    pub description: String,
    pub issue_type: IssueType,
    pub points: f64,
    pub components: Vec<Component>,
    pub status: Status,
    pub resolution_date: Option<DateTime<Utc>>,
    pub created: DateTime<Utc>,
    pub assignee: Option<Person>,
    pub updated: DateTime<Utc>
}

//     fn map_components(val:Value) {
//         let comps = &val["fields"]["components"].;

//     }

pub fn vstr_or(v:&Value, s:&str) -> String {
    String::from(v.as_str().unwrap_or(s))
}

pub fn vstr(v:&Value) -> String { vstr_or(v, "") }

pub fn vi64(v:&Value) -> i64 {
    v.as_str().unwrap_or("0").parse::<i64>().unwrap_or(0i64)
}

pub fn points(v:&Value) -> f64 {
    println!("points raw: {:?}", v);
    match v {
        Value::Number(n) => n.as_f64().unwrap_or(0f64),
        _ => 0f64
    }
}

impl Issue {
    pub fn from_value(raw:&str) -> Issue {
        let val:Value = serde_json::from_str(raw).unwrap();
        let fields = &val["fields"];
        let issue = Issue {
            key: vstr(&val["key"]),
            id: vi64(&val["id"]),
            summary: vstr(&fields["summary"]),
            description: vstr(&fields["description"]),
            points: points(&fields["customfield_10002"]),
            issue_type: IssueType::from_string(&vstr(&fields["issuetype"]["name"])),
            components: Vec::default(),
            status: Status::from_string(&vstr(&fields["status"]["name"])),
            resolution_date: None,
            created: Utc::now(),
            assignee: Person::from_value_opt(&fields["assignee"]),
            updated: Utc::now()
        };
        issue
    }
}