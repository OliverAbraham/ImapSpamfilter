# IMAP SPAMFILTER

## OVERVIEW


This is a Spamfilter for IMAP postboxes.
It works independently from your email client.
It will work with every mail server, as it need no special functionality.


## FUNCTIONING

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
An email is spam when:
    - if the sender contains one of the sender blacklist words (you can whitelist all senders of a domain, e.g. "@mydomain.com")
    - if more than half of the subject characters are non-latin (hard coded)
    - if the sender email address contains more than a given number of special characters (configurable)
    - if the sender address without punctuation contains more than a given number of special characters (configurable)
    - if the subject contains one of the subject blacklist words
    

## A NOTE ON OUTLOOK
If you use outlook as imap mail client on your computer, it will automatically update its content 
after some time, depending on the reload cycle.
Normally you will see the spam mail disappering from your inbox, quite after the spamfilter had 
classified an mail as spam. 
And some minutes later at your spam folder, the number of unread emails will increase. 
Outlook refreshes the subscribed imap folders o a regular basis, but slower than the inbox update.


## AUTHOR
Written by Oliver Abraham, mail@oliver-abraham.de


## INSTALLATION AND CONFIGURATION
An installer is not provided
- Build the application
- Edit appsettings.hjson (basic settings)
- Edit spamfilter-configuration.hjson (filter rules)
- Edit nlog.config only if necessary. You can set the log rotation here.


## LICENSE
This project is licensed under Apache license.


## SOURCE CODE
https://www.github.com/OliverAbraham/Spamfilter


## AUTHOR
Oliver Abraham, mail@oliver-abraham.de

