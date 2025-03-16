var searchingContacts = false;
var timeoutId = 0;
var balance = "0";
var fiatBalance = "0";
var hideBalance = false;
var selectedItemId = null;

var homeModal = document.getElementById('homeMenuModal');

function onMainMenuAction() {
    leftSidebar.style.display = "block";
}

function onMainMenuClose() {
    leftSidebar.style.display = "none";
}

function onHomeMenuAction()
{
    homeModal.style.display = "block";
}

function onHomeMenuClose()
{
    homeModal.style.display = "none";
}

// Handle modals outside tap
window.onclick = function(event) {
    if (event.target == homeModal) {
        onHomeMenuClose();
    }
    else if (event.target == leftSidebar) {
        leftSidebar.style.display = "none";
    }
}

function setVersion(version) {
    document.getElementById("version").innerHTML = version;
}

document.getElementById("activity_balance_toggle").onclick = function () {
    toggleBalance();
}

// Toolbar
document.getElementById("MainMenu").onclick = function() {
    onMainMenuAction();
}

document.getElementById("filter-all").onclick = function () {
    location.href = "ixian:filter:all";
}
document.getElementById("filter-sent").onclick = function () {
    location.href = "ixian:filter:sent";
}
document.getElementById("filter-received").onclick = function () {
    location.href = "ixian:filter:received";
}

function displayBalance(hideBalance) {
    const balanceElement = document.getElementById('activity_balance_number');
    const fiatBalanceElement = document.getElementById('activity_balance_info');
    const toggleElement = document.getElementById('activity_balance_toggle');

    if (!hideBalance) {
        balanceElement.innerHTML = balance;
        fiatBalanceElement.innerHTML = fiatBalance;
        toggleElement.innerHTML = SL_IndexBalanceHide + " <i class='fa fa-eye'></i>";
    } else {
        balanceElement.innerHTML = "<img class=\"ixicash-icon\" src=\"img/ixilogo.svg\"/> <span>--</span>";
        fiatBalanceElement.innerHTML = SL_IndexBalanceInfo;
        toggleElement.innerHTML = SL_IndexBalanceShow + " <i class='fa fa-eye-slash'></i>";
    }
}

function setBalance(bal, fiatBal, theNick) {
    // Set the user nickname
    document.getElementById('menu_nickname').innerHTML = theNick;

    bal = amountWithCommas(bal);
    fiatBal = amountWithCommas(fiatBal);
    balance = "<img class=\"ixicash-icon\" src=\"img/ixilogo.svg\"/> <span>" + bal + "</span>";
    fiatBalance = "$" + fiatBal;

    if (hideBalance == false) {
        displayBalance(false);
    }
}

function toggleBalance()
{
    hideBalance = !hideBalance;
    displayBalance(hideBalance);
    location.href = `ixian:balance:${hideBalance ? 'hide' : 'show'}`;
}

function setHideBalance(hide)
{
    hideBalance = hide === "True";
    displayBalance(hideBalance);
}

function selectFABOption(link) {
    onHomeMenuClose();
    location.href = "ixian:" + link;
}

function selectMenuOption(link) {
    location.href = "ixian:" + link;
}

function loadAvatar(avatar_path) {
    avatar_path = avatar_path.replace(/&#92;/g, '\\');

    var av_path = avatar_path + "?t=" + new Date().getTime();
    document.getElementById("avatar_image").src = av_path;
}

// Filter in the CONTACTS tab
function contactSearch()
{
    var a,b,i,row;
    var input = document.getElementById('contactInput');
    var filter = input.value.toUpperCase();
    var c_contactlist = document.getElementById("contactslist");
    var c_items = c_contactlist.getElementsByClassName('spixi-list-item');

    if (isBlank(filter) == true) {
        searchingContacts = false;
    }
    else {
        searchingContacts = true;
    }

    // Go through each element and filter out non-matching elements
    for (i = 0; i < c_items.length; i++) {
        row = c_items[i].getElementsByTagName("div")[0];
        b = row.getElementsByTagName("div")[2];
        a = b.getElementsByTagName("div")[0];

        if (a.innerHTML.toUpperCase().indexOf(filter) > -1) {
            c_items[i].style.display = "";
        } else {
            c_items[i].style.display = "none";
        }
    }
}

// Clears all contacts from contacts page
function clearContacts()
{
    if (searchingContacts == true) {
        return;
    }

    var contactsNode = document.getElementById("contactslist");
    while (contactsNode.firstChild) {
        contactsNode.removeChild(contactsNode.firstChild);
    }
}

// Adds a contact to the contacts page
function addContact(wal, name, avatar, online, unread)
{
    if (searchingContacts == true) {
        return;
    }

    var indicator = " offline";
    if (online == "true") {
        indicator = " online";
    }

    var unreadIndicator = "";
    if (unread > 0) {
        unreadIndicator = " unread";
    }

    var contactsNode = document.getElementById("contactslist");

    var contactEntry = document.createElement("div");
    contactEntry.id = "c_" + wal;
    contactEntry.className = "spixi-list-item" + indicator + unreadIndicator;
    contactEntry.innerHTML = '<a href="ixian:details:' + wal + '"><div class="row"><div class="col-2 spixi-list-item-left"><img class="spixi-list-item-avatar" src="' + avatar + '"/><div class="spixi-friend-status-indicator"></div></div><div class="col-8 spixi-list-item-center"><div class="spixi-list-item-title-center nick">' + name + '</div></div><div class="col-2 spixi-list-item-right"><div class="spixi-chat-unread-indicator"></div></div></div></a>';

    contactsNode.appendChild(contactEntry);
}

function setContactStatus(wal, online, unread, excerpt, msgTimestamp)
{
    // Update for contacts
    var el = document.getElementById("c_" + wal);
    if(el == null)
    {
        return;
    }
    
    var indicator = " offline";
    if (("" + online).toLowerCase() == "true") {
        online = "true";
        indicator = " online";
    }

    var unreadIndicator = "";
    if (unread > 0) {
        unreadIndicator = " unread";
    }
    el.className = "spixi-list-item" + indicator + unreadIndicator;
    
    // update for chats
    var chatEl = document.getElementById("ch_" + wal);
    if(chatEl != null)
    {
        chatEl.className = "spixi-list-item" + indicator + unreadIndicator;
        if (selectedItemId == chatEl.id) {
            chatEl.className += " selected";
        }
    }

    var nickEl = el.getElementsByClassName("nick");
    var nick = nickEl[0].innerHTML;
    var avatarEl = el.getElementsByClassName("spixi-list-item-avatar");
    var avatarSrc = avatarEl[0].src;

}


// Clears payment activity from wallet page
function clearPaymentActivity(filter)
{
    var paymentsNode = document.getElementById("paymentlist");
    while (paymentsNode.firstChild) {
        paymentsNode.removeChild(paymentsNode.firstChild);
    }

    updateFilterButton("filter-all", "");
    updateFilterButton("filter-sent", "");
    updateFilterButton("filter-received", "");

    if (filter == "all")
        updateFilterButton("filter-all", "active");
    else if (filter == "sent")
        updateFilterButton("filter-sent", "active");
    else if (filter == "received")
        updateFilterButton("filter-received", "active");       
}

function updateFilterButton(btnid, filter)
{
    document.getElementById(btnid).className = "spixi-activity-filterbutton " + filter;
}

// Adds a payment
function addPaymentActivity(txid, receive, text, timestamp, amount, fiatAmount, confirmed)
{
    document.getElementById("section_transactions").style = "display:block";
    document.getElementById("section_no_transactions").style = "display:none";


    let iconClass, icon;
    switch (confirmed) {
        case "true":
            iconClass = "spixi-status-green";
            icon = 'fa fa-check-circle';
            break;
        case "error":
            iconClass = "spixi-status-red";
            icon = 'fa fa-exclamation-circle';
            break;
        default:
            iconClass = "spixi-status-yellow";
            icon = 'fa fa-spinner fa-spin';
    }


    const isReceived = receive === "1";
    const arrow = `<i class="spixi-list-tx-icon ${isReceived ? "spixi-tx-green" : "spixi-tx-red"} fa fa-arrow-${isReceived ? "down" : "up"}"></i>`;
    const signedAmount = `${isReceived ? '+' : '-'} ${amountWithCommas(amount)}`;


    const fiatAmountText = `$${amountWithCommas(fiatAmount)}`;


    const paymentEntry = document.createElement("div");
    paymentEntry.id = "tx_" + txid;
    paymentEntry.className = "spixi-list-item nopad";
    if (selectedItemId == paymentEntry.id) {
        paymentEntry.className += " selected";
    }
    paymentEntry.innerHTML = `
        <a href="ixian:txdetails:${txid}">
            <div class="row no-gutters spixi-list-item-first-row flex-nowrap">
                <div class="col">
                    <div class="spixi-list-item-from"><i class="spixi-list-item-from-status ${iconClass} ${icon}"></i> ${text}</div>
                </div>
                <div class="col spixi-list-item-right">
                    <div class="spixi-list-item-amount">${signedAmount} ${arrow}</div>
                </div>
            </div>
            <div class="row no-gutters spixi-list-item-second-row flex-nowrap">
                <div class="col">
                    <div class="spixi-list-item-timestamp">${timestamp}</div>
                </div>
                <div class="col spixi-list-item-right">
                    <div class="spixi-list-item-amount-fiat">${fiatAmountText}</div>
                </div>
            </div>
        </a>`;


    document.getElementById("paymentlist").appendChild(paymentEntry);
}


function setupCloneNode(element) {
    if (element.id) {
        element.id = element.id + '-clone';
    }
    for (let i = 0; i < element.children.length; i++) {
        setupCloneNode(element.children[i]);
    }
}

// Clears all chats from chats page
function clearChats() {
    // Get the original chatlist div
    const chatlist = document.getElementById('chatlist');
    const chatlistClone = chatlist.cloneNode(true);
    setupCloneNode(chatlistClone);
    chatlist.parentNode.insertBefore(chatlistClone, chatlist.nextSibling);
    //chatlist.style.visibility = 'hidden';
    chatlist.style.display = 'none';
    chatlist.innerHTML = '';
}


function clearChatsDone() {
    const chatlist = document.getElementById('chatlist');
    const chatlistClone = document.getElementById('chatlist-clone');

    if (chatlistClone) {
        chatlistClone.style.display = 'none';
        chatlistClone.style.visibility = 'hidden';
        chatlistClone.parentNode.removeChild(chatlistClone);
    }
    //chatlist.style.visibility = 'visible';
    chatlist.style.display = 'block';

    if (!chatlist.hasChildNodes()) {
        document.getElementById("chat_no_activity").style.display = 'block';
        document.getElementById("chat_action_button").style.display = 'none';
    }
}

function selectChat(wallet) {
    var id = "ch_" + wallet;
    selectedItemId = id;

    var items = document.querySelectorAll('.spixi-list-item');
    items.forEach(function (item) {
        item.classList.remove('selected');
    });

    const item = document.getElementById(selectedItemId);
    if (item) {
        item.classList.add('selected');
    }
}

function selectTx(wallet) {
    var id = "tx_" + wallet;
    selectedItemId = id;

    var items = document.querySelectorAll('.spixi-list-item');
    items.forEach(function (item) {
        item.classList.remove('selected');
    });
    
    const item = document.getElementById(selectedItemId);
    if (item) {
        item.classList.add('selected');
    }
}

// Adds a chat
function addChat(wallet, from, timestamp, avatar, online, excerpt_msg, type, unread, insertToTop)
{
    var excerpt = excerpt_msg;

    let indicator = online === "true" ? " online" : " offline";

    var unreadIndicator = "";
    var readIndicator = "";

    switch (type) {
        case "read":
            readIndicator = '<i class="spixi-chat-read-indicator spixi-chat-read-indicator-read fas fa-check-double"></i>';
            break;
        case "confirmed":
            readIndicator = '<i class="spixi-chat-read-indicator spixi-chat-read-indicator-confirmed fas fa-check"></i>';
            break;
        case "pending":
            readIndicator = '<i class="spixi-chat-read-indicator spixi-chat-pending-indicator fas fa-clock"></i>';
            break;
        case "default":
            readIndicator = '<i class="spixi-chat-read-indicator spixi-chat-default-indicator fas fa-comment-slash"></i>';
            break;
    }

    if (unread > 0) {
        unreadIndicator = " unread";
        readIndicator = "";
    }
    
    var excerpt_style = type === "typing" ? "typing" : "";

    var timeClass = "spixi-timestamp";
    var relativeTime = getRelativeTime(timestamp);

    if (getTimeDifference(timestamp) < 3600) {
        timeClass = "spixi-timestamp spixi-rel-ts-active";
    }

    var readmsg = document.createElement("div");
    readmsg.id = "ch_" + wallet;
    readmsg.className = "spixi-list-item" + indicator + unreadIndicator;
    if (selectedItemId == readmsg.id) {
        readmsg.className += " selected";
    }

    readmsg.innerHTML = `
        <a href="ixian:chat:${wallet}">
            <div class="row flex-nowrap">
                <div class="col-2 spixi-list-item-left">
                    <img class="spixi-list-item-avatar" src="${avatar}"/>
                    <div class="spixi-friend-status-indicator"></div>
                </div>
                <div class="col-6 spixi-list-item-center">
                    <div class="spixi-list-item-title">${from}</div>
                    <div class="spixi-list-item-subtitle ${excerpt_style}">${excerpt_msg}</div>
                </div>
                <div class="col-4 spixi-list-item-right">
                    <div class="spixi-chat-unread-indicator"></div>
                    ${readIndicator}
                    <div class="${timeClass}" data-timestamp="${timestamp}">${relativeTime}</div>
                </div>
            </div>
        </a>`;

    var chatsNode = document.getElementById("chatlist");
    if(insertToTop)
    {
        chatsNode.insertBefore(readmsg, chatsNode.firstElementChild);
    }else
    {
        chatsNode.appendChild(readmsg);
    }

    document.getElementById("chat_no_activity").style.display = 'none';
    document.getElementById("chat_action_button").style.display = 'block';
}

function setUnreadIndicator(unread_count) {
    var dot = document.getElementById("unread-dot");
    if (unread_count != "0") {
        dot.style.display = "block";
    } else {
        dot.style.display = "none";
    }
}

// Clears all unread activity from main page
function clearUnreadActivity() {

}

function addUnreadActivity(wallet, from, timestamp, avatar, online, excerpt_msg, insertToTop) {
    var excerpt = excerpt_msg;

    var indicator = " offline";
    if (online == "true") {
        indicator = " online";
    }

    var timeClass = "spixi-timestamp";
    var relativeTime = getRelativeTime(timestamp);

    if (getTimeDifference(timestamp) < 3600) {
        timeClass = "spixi-timestamp spixi-rel-ts-active";
    }

    document.getElementById("chatlist").style.display = 'block';
    document.getElementById("chat_no_activity").style.display = 'none';
}

function setNotificationCount(notification_count) {
    document.getElementById("notification_count").innerHTML = notification_count;
}

// Function to toggle tab's active color
$('a[data-toggle="tab"]').on('shown.bs.tab', function (e) {
    // Not very elegant, but it works
    document.getElementById("tab1").className = "col-3 spixi-tab";
    document.getElementById("tab2").className = "col-3 spixi-tab";
    document.getElementById("tab3").className = "col-3 spixi-tab";
    document.getElementById("tab4").className = "col-3 spixi-tab";

    var cl = "active";
    e.target.parentElement.className = "col-3 spixi-tab " + cl;

    var tabid = e.target.parentElement.id;
    location.href = "ixian:tab:" + tabid;
});



// Handle sidebar swiping
$("#leftSidebarHelper").swipe( {
    swipeStatus:function(event, phase, direction, distance)
    {
        var str = "";
        if(direction == "right")
        {
            leftSidebar.style.display = "block";
            document.getElementById('leftSidebarHelper').style.display = "none";

            leftSidebar.style.right = "0px";
            document.getElementById("version").style.left = "0px";
        }
    },
    threshold:10
});

$("#leftSidebar").swipe( {
    swipeStatus:function(event, phase, direction, distance)
    {
        if(direction == "left")
        {
            if (phase=="move")
            {
                leftSidebar.style.left = "-" + distance + "px";
                leftSidebar.style.right = distance + "px";
                document.getElementById("version").style.left = "-" + distance + "px";
            }

            if (phase == "end" || phase == "cancel")
            {
                if(distance > 100)
                {
                    leftSidebar.style.display = "none";
                    document.getElementById('leftSidebarHelper').style.display = "block";
                }
                leftSidebar.style.left = "0px";
                leftSidebar.style.right = "0px";
                document.getElementById("version").style.left = "0px";
            }
        }
    },
    triggerOnTouchEnd:true,
    threshold:100,
    allowPageScroll:"vertical"
});

function showRatingPrompt() {
    document.getElementById("ratingModal").style.display = "block";
}

document.getElementById("rating_no").onclick = function () {
    document.getElementById("ratingModal").style.display = "none";
    location.href = "ixian:rating:no";
}
document.getElementById("rating_yes").onclick = function () {
    document.getElementById("ratingModal").style.display = "none";
    location.href = "ixian:rating:yes";
}


function selectTab(tab) {
    $("#" + tab + " > a").tab('show');
}


var logoClicked = 0;

function countLogoClick()
{
    logoClicked++;
    if(logoClicked > 10)
    {
        document.getElementById("SendLogMenuItem").style.display = "block";
        alert(SL_DevMode);
    }
}