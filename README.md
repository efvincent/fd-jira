# FD Jira
Build an event stream from Jira ticket data

## What?
Figure out a way to go to the Jira API and turn whatever we find there (history? audit logs? static snapshots? reports?) into a series of events in the event db style (ie change/delta events).

## Why?
The idea is if we had Jira data in a stream, we could then project it into various data stores for more interesting reporting and analysis.

## Jira API
This is a typical rest call:
```
 https://jira.walmart.com/rest/api/2/issue/RCTFD-4223\?fields\=changelog\&expand\=changelog
```

## Some links
* [Jira API v2 Documentation](https://developer.atlassian.com/cloud/jira/platform/rest/v2)
* [Serde Rust JSON Docs](https://docs.serde.rs/serde_json/index.html)
* [Serde Crate](https://crates.io/crates/serde)
* [Rust by Example](https://doc.rust-lang.org/stable/rust-by-example): it's just useful
* [OsStr Docs](https://doc.rust-lang.org/std/ffi/struct.OsStr.html) Understanding how Rust handles platform differences when retrieving things like environment variables.
