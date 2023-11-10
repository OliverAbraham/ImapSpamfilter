# IMAP SPAMFILTER

![](https://img.shields.io/github/downloads/oliverabraham/ImapSpamfilter/total) ![](https://img.shields.io/github/license/oliverabraham/ImapSpamfilter) ![](https://img.shields.io/github/languages/count/oliverabraham/ImapSpamfilter) ![GitHub Repo stars](https://img.shields.io/github/stars/oliverabraham/ImapSpamfilter?label=repo%20stars) ![GitHub Repo stars](https://img.shields.io/github/stars/oliverabraham?label=user%20stars)


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


## AUTHOR
Written by Oliver Abraham, mail@oliver-abraham.de


## INSTALLATION
An installer is not provided
- Build the application or download the latest release


## INSTALLATION AND CONFIGURATION
#### Edit the file "appsettings.hjson"

Just check the path to the file "spamfilter-configuration.hjson". 
Normally just name the file, without any path-


#### Edit the filter rules 

Edit file "spamfilter-configuration.hjson".
First, enter the credentials for your mailbox like this.
If you don't want to forward emails, you can leave out the Smtp lines:

    MailAccounts: [
	{
        Name                            : "My email account",
        ImapServer			    : "imap.yourprovider.com",
        ImapPort			    : "993",
        Username			    : "ENTER_YOUR_ACCOUNT_HERE____TYPICALLY_YOUR_EMAIL_ADDRESS",
        Password			    : "ENTER_YOUR_MAILBOX_PASSWORD_HERE",
        SmtpServer                      : "smtp.yourprovider.com",
        SmtpPort                        : 465,
        SmtpUsername                    : "ENTER_YOUR_ACCOUNT_HERE____TYPICALLY_YOUR_EMAIL_ADDRESS",
        SmtpPassword                    : "ENTER_YOUR_MAILBOX_PASSWORD_HERE",
        InboxFolderName                 : "inbox",
        Rules: [
        { ... enter your rules here ...}
        ]
	}
    ],


This is an example for a rule that checks every incoming email for spam. Spam is moved to the junk folder:

	Rules: [
    {                             
    	Name              	: "Spamcheck",
    	IfMailIsSpam      	: true,
    	Actions           	: [ { Type: "MoveToFolder", Folder: "junk" } ],
    	StopAfterAction         : true,
    	SpamfilterSettings      : {}
	},



The following example is an additional rule that checks for special words in the email. 
This makes only sense to process emails from a web form that don't use captchas.
I assume your web form has a choice field with several choices "I am no human", and one choice "i am a human".
A robot won't fill out the correct choice, so you can recognize spam with "no human".

	Rules: [
    { . . . },
    {                             
    	Name              			: "Spamcheck for contact form",
    	IfMailIsSpam      			: false,
    	IfMailWasSentBy    			: ["sender@mywebsite.com"],
    	IfMailWasSentTo    			: ["mail@myemailaddress.com"],
    	IfMailContainsWordsInSubject            : ["Kontact form"],
    	IfMailContainsWordsInBody               : ["no human"" ],
    	Actions           			: [ { Type: "MoveToFolder", Folder: "junk" } ],
    	StopAfterAction                         : true,
    	SpamfilterSettings			: {}
    },

My last example has for rules:
- It checks for spam using your rules and spamhaus
- checks for the word "no human" in the body
- If the mail is good, it worwards it to another mailbox
- and moves it to the folder "ForwaredEmails"

Like this:

	Rules: [
    {                             
    	Name              			: "Spamcheck",
    	IfMailIsSpam      			: true,
    	Actions           			: [ { Type: "MoveToFolder", Folder: "junk" } ],
    	StopAfterAction                         : true,
    	SpamfilterSettings			: {}
    },
    {                             
    	Name              			: "Spamcheck for contact form",
    	IfMailIsSpam      			: false,
    	IfMailWasSentBy    			: ["sender@mywebsite.com"],
    	IfMailWasSentTo    			: ["mail@myemailaddress.com"],
    	IfMailContainsWordsInSubject            : ["Kontaktformular"],
    	IfMailContainsWordsInBody               : ["no human" ],
    	Actions           			: [ { Type: "MoveToFolder", Folder: "junk" } ],
    	StopAfterAction                         : true,
    	SpamfilterSettings			: {}
    },
    {
    	Name              			: "Forward ciridata emails",
    	Actions           			: [ { Type: "Forward", Receiver: "mail@another.com" } ],
    	StopAfterAction                         : false,
    },
    {
    	Name              			: "move to forwarded folder",
    	Actions           			: [ { Type: "MoveToFolder", Folder: "ForwardedEmails" } ],
    	StopAfterAction                         : false,
    }


#### Edit "nlog.config" (optional)

To change my default logging settings, edit this file.
I've configured weekly log rotation and two files, one for debug messages and one for the spam related actions.
If you're not familiar with nlog, leave the file as it is.
For more information refer to https://nlog-project.org/


## LICENSE
This project is licensed under Apache license.


## CREDITS
Thanks to Marlin Brüggemann for the Spamhaus query code!
He published the code under https://github.com/mabru47/Spamhaus.Net


## SOURCE CODE
https://www.github.com/OliverAbraham/Spamfilter


## AUTHOR
Oliver Abraham, mail@oliver-abraham.de


# MAKE A DONATION !

If you find this application useful, buy me a coffee!
I would appreciate a small donation on https://www.buymeacoffee.com/oliverabraham

<a href="https://www.buymeacoffee.com/app/oliverabraham" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me A Coffee" style="height: 60px !important;width: 217px !important;" ></a>
