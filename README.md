#FD Jira
Build an event stream from Jira ticket data

##What?
Figure out a way to go to the Jira API and turn whatever we find there (history? audit logs? static snapshots? reports?) into a series of events in the event db style (ie change/delta events).

##Why?
The idea is if we had Jira data in a stream, we could then project it into various data stores for more interesting reporting and analysis.