use chrono::prelude::*;

#[derive(Debug)]
pub enum IssueType {
  Story,
  Epic
}

#[derive(Debug)]
pub enum Component {
  Mojo,
  Phoenix,
  Wolverine,
  Ironman,
  Product,
  Design
}

#[derive(Debug)]
pub enum Status {
  Backlog,
  ReadyForWork,
  Active,
  Done,
  Deleted
}

#[derive(Debug)]
pub struct Person {
  pub key: String,
  pub email: String,
  pub name: String
}

#[derive(Debug)]
pub struct Issue {
    pub key: String,
    pub summary: String,
    pub description: String,
    pub issue_type: IssueType,
    pub components: Vec<Component>,
    pub status: Status,
    pub resolution_date: DateTime<Utc>,
    pub created: DateTime<Utc>,
    pub assignee: Option<Person>,
    pub priority: u32,
    pub updated: DateTime<Utc>
}