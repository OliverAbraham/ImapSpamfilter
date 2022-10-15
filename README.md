# IMAP SPAMFILTER

## OVERVIEW

This is a Spamfilter for IMAP postboxes, working independently from your email client.
It will work with every mail server, no special special functionality needed.
It will even work on a server and doesn't need to run on your client.
This opens up more possibilities.


## FUNCTION

It will connect periodically to your imap mail server and check every new(unread) email in your inbox.
If it's classified as spam, it will move it to a folder in your postbox.

The filter rules are configured in a hjson file. (see my example)
Its able to classify any given email by a set of rules.
Rules are basic now. They are three lists and some fixed rules.
The lists are: 
    - a sender white list
    - a sender black list 
    - a subject black list

The rules are processed in the following order.
An email is spam:
    - if the sender is blacklisted by spamhaus
    - if the sender contains one of the sender blacklist words (you can whitelist all senders of a domain, e.g. "@mydomain.com")
    - if more than half of the subject characters are non-latin (hard coded)
    - if the sender email address contains more than a given number of special characters (configurable)
    - if the sender address without punctuation contains more than a given number of special characters (configurable)
    - if the subject contains one of the subject blacklist words
    

## A NOTE ON EMAIL CLIENTS
Configure your email clients to use imap, don't use POP3 protocol.
My Spam filter works in the postbox on the server.
If you use POP3 als protocol, you will always have a copy of your inbox on the mail server.
So when an email is identified as spam, it ill be moved to a certain folder on the mail server.
Your POP3 client will not be able to recognize that.
So therefore use IMAP as transport protocol.
    

## USING OUTLOOK
If you use outlook as imap mail client on your computer, it will automatically update its content 
after some time, depending on the reload cycle.
Normally you will see the spam mail disappering from your inbox, a few seconds after the spamfilter 
had classified an mail as spam. 
You local spam folder in outlook will update some time later, because outlook does these updates
on a different schedule.
Nevertheless, the number of unread emails will increase in your spam folder. 


## A NOTE ON THE CONFIGURATION FILE
There's no UI for the config file yet. My Spam filter will recognize if you change the file and save it.
When the last write time of this file changes, it will reload the file and re-process your unread emails.
So this is convenient: When you adjust the spam rules, you only have to wait.


## A NOTE ON THE CONFIGURATION FILE 2
The file format is open to support rules in general, like outlook rules. 
Please note my spam filter is not perfect, just an MVP.


## AUTHOR
Written by Oliver Abraham, mail@oliver-abraham.de


## INSTALLATION AND CONFIGURATION
An installer is not provided
- Build the application
- Edit appsettings.hjson (basic settings)
- Edit spamfilter-configuration.hjson (filter rules)
- Edit nlog.config (optional) You can set the log rotation here, for example). For more information refer to https://nlog-project.org/


## LICENSE
This project is licensed under Apache license.


## CREDITS
Thanks to Marlin Brüggemann for the Spamhaus query code!
He published the code under https://github.com/mabru47/Spamhaus.Net


## SOURCE CODE
https://www.github.com/OliverAbraham/Spamfilter


## AUTHOR
Oliver Abraham, mail@oliver-abraham.de

