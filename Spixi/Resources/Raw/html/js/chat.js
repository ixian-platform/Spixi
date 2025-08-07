var notifications = false;
var isBot = false;
var isAdmin = false;
var messageCost = "";

var requestReceivedModal = document.getElementById("requestReceivedModal");
var requestSentModal = document.getElementById("requestSentModal");
var reportModal = document.getElementById("reportModal");
var reportMsgId = "";

var userNick = "";
var userAddress = "";

const attachBar = document.getElementById("attach-bar");

const messagesEl = document.getElementById("messages");
const chatHolderEl = document.getElementById("chatholder");
const wrapEl = document.getElementById("wrap");

function onChatScreenLoad() {

    if (SL_Platform == "Xamarin-WPF") {
        document.getElementById("chat_input").focus();
        messagesEl.oncontextmenu = function (e) {
            hideContextMenus();
            displayContextMenu(e);
            e.stopPropagation();
            e.preventDefault();
            return false;
        };
    } else {
        messagesEl.ondblclick = function (e) {
            hideContextMenus();
            displayContextMenu(e);
            e.stopPropagation();
            e.preventDefault();
            return false;
        };
    }


    messagesEl.addEventListener("click", function (e) {
        hideAttachBar();

        if (e.target.className.indexOf("nick") != -1) {
            var nickEl = e.target;
            var nick = nickEl.getAttribute("nick");
            var address = nickEl.getAttribute("address");
            if (address != nick) {
                if (nickEl.innerHTML == nick) {
                    nickEl.innerHTML = address;
                } else {
                    nickEl.innerHTML = nick;
                }
            }
        }
    });

    document.body.oncontextmenu = function (e) {
        hideContextMenus();
    };

    onload();
}

function hideAttachBar(){
    if (attachBar && attachBar.classList.contains("show")) {
        attachBar.classList.remove("show");
        attachBar.classList.add("hide");
    }
}

function hideContextMenus() {
    hideChannelSelector();
    hideContextMenu();
}

function setBotMode(bot, cost, costText, admin, botDescription, notificationsString) {
    var isPaid = false;
    if (admin == "True") {
        isAdmin = true;
        document.getElementsByClassName("spixi-bot-user-actions")[0].style.display = "block";
    } else {
        isAdmin = false;
        document.getElementsByClassName("spixi-bot-user-actions")[0].style.display = "none";
    }

    messageCost = cost;
    var payBar = document.getElementById("SpixiPayableBar");
    if (payBar != null) {
        document.body.removeChild(payBar);
    }

    if (messageCost != "0.00000000") {
        var msgEl = document.createElement("div");
        msgEl.id = "SpixiPayableBar";
        msgEl.className = "spixi-chat-payable-bar";
        msgEl.innerHTML = "<span><i class='fa fa-info-circle'></i></span> " + costText;

        document.body.appendChild(msgEl);
        isPaid = true;
    }

    if (bot == "True") {
        isBot = true;
        document.getElementsByClassName("spixi-toolbar-holder")[0].className = "spixi-toolbar-holder bot";
        document.getElementsByClassName("spixi-channel-bar")[0].style.display = "table";

        if (isPaid) {
            messagesEl.style.height = "calc(100vh - 175px)";
        } else {
            messagesEl.style.height = "calc(100vh - 150px)";
        }

    } else {
        isBot = false;
        document.getElementsByClassName("spixi-toolbar-holder")[0].className = "spixi-toolbar-holder";
        document.getElementsByClassName("spixi-channel-bar")[0].style.display = "none";
    }

    if (notificationsString == "True") {
        notifications = true;
        document.getElementsByClassName("spixi-bot-notifications-toggle")[0].className = "spixi-switch spixi-bot-notifications-toggle";
    } else {
        notifications = false;
        document.getElementsByClassName("spixi-bot-notifications-toggle")[0].className = "spixi-switch off spixi-bot-notifications-toggle";
    }

    document.getElementsByClassName("spixi-bot-description")[0].innerHTML = botDescription;
}

var selectedChannel = 0;

function setSelectedChannel(id, icon, name) {
    selectedChannel = id;
    var channelBarEl = document.getElementsByClassName("spixi-channel-bar")[0];
    channelBarEl.getElementsByClassName("channel-icon")[0].innerHTML = "<i class=\"fa " + icon + "\"></i>";
    channelBarEl.getElementsByClassName("channel-name")[0].innerHTML = name + "<div class=\"unread-indicator\"></div>";
}

function isScrollable(el) {
    return el.scrollHeight > el.clientHeight;
}

function setInitialStickyDate() {
    const bubbles = document.querySelectorAll(".spixi-bubble");
    if (bubbles.length === 0) return;

    const firstVisibleBubble = bubbles[bubbles.length - 1];
    const timestampEl = firstVisibleBubble.querySelector(".time[data-timestamp]");
    if (!timestampEl) return;

    const time = parseInt(timestampEl.getAttribute("data-timestamp"), 10);
    const date = new Date(time * 1000);

    const stickyHeader = document.getElementById("sticky-date");
    if (stickyHeader && isScrollable(messagesEl)) {
        stickyHeader.textContent = formatDateForStickyDate(date);
        stickyHeader.style.display = "flex";
    } else{
        stickyHeader.style.display = "none";
    }
}

function onChatScreenLoaded() {
    debouncedScrollToBottom();
    if (SL_Platform == "Xamarin-iOS") {
        debouncedIOSFixer();
    } else if (SL_Platform == "Xamarin-WPF") {
        document.getElementById("chat_input").focus();
    }

    updateChatInputPlaceholder();

    //TODO: this is just an example for adding session/invite banner in chat screen
    // we should check if the user is in session or if an invite was sent
    // set to TRUE to test it out

    const isInviteSent = false;
    const isInSession = false;

    if (isInSession) {
        const elementToAdd = `<section class="session-banner">
                <div class="spixi-session-banner-content">
                    <span class="body-sm">${SL_InSessionWith} USERNAME</span>
                    <span class="label-sm">${SL_TapToReturn} <i class="fas fa-arrow-right"></i></span>
                </div>
                <div class="spixi-flat-button label-sm button outline" onclick="showEndAppSessionModal()">${SL_Leave}</div>
            </section>`;
        messagesEl.insertAdjacentHTML("afterbegin", elementToAdd);
    } else if(isInviteSent){
        const elementToAdd = ` <section class="session-banner">
                <span class="label-sm"><i class="fas fa-user-plus"></i>&nbsp;${SL_InviteSentTo} USERNAME</span>
                <div class="spixi-flat-button label-sm button outline">${SL_CancelInvite}</div>
            </section>`;
        messagesEl.insertAdjacentHTML("afterbegin", elementToAdd);
    }
}

function showEndAppSessionModal(){
    const modal = document.getElementById("endAppSessionModal");
    modal.style.display = "flex";
}

function onChatScreenReady(address) {
    userAddress = address;
    userNick = address;
    setBotAddress(address);
}

function hideBackButton() {
    var backBtn = document.getElementById("backbtn");
    var placeholder = document.createElement("div");
    placeholder.id = "backbtn";
    placeholder.style.width = "12px";
    placeholder.style.minWidth = "12px";
    backBtn.parentNode.replaceChild(placeholder, backBtn);

    document.getElementById("left-separator").style.display = "block";
}

document.getElementById("backbtn").onclick = function () {
    if (document.getElementById("ContactsBox")) {
        hideContacts();
    } else if (document.getElementById("BotDetails").style.display == "block") {
        hideBotDetails();
    } else {
        location.href = "ixian:back";
    }
}

function test() {
    setNickname("TesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTesterTester");
    setOnlineStatus("Online");
    showCallButton();
    showContactRequest(true);
    /*   addMe(0, "img/spixiavatar.png", "Hi!", "11:23 AM");
       addFile(10, 9090, "img/spixiavatar.png", "file1.png", "10:23 AM", false, false, false);
       setTimeout(function () { updateFile(9090, "25", "False"); }, 1000);
       setTimeout(function () { updateFile(9090, "50", "False"); }, 2000);
       setTimeout(function () { updateFile(9090, "75", "False"); }, 3000);
       setTimeout(function () { updateFile(9090, "100", "False"); }, 4000);

       setTimeout(function () { addCall(1000, "Incoming call", "Duration 15:32", "False", "11:23 AM"); }, 1000);
       setTimeout(function () { addCall(1000, "Incoming call", "Declined", "True", "11:23 AM"); }, 2000);

       setTimeout(function () { addMe(1, "aaa", "me", "img/spixiavatar.png", "How are you today? &#x1f602", "11:23 AM", "True", "True", "False"); }, 1000);
       setTimeout(function () { addThem(2, "img/spixiavatar.png", "Hey! &#x1f604", "11:24 AM", "True", "True"); }, 1300);
       setTimeout(function () { addThem(3, "img/spixiavatar.png", "Great, thanks for asking.", "11:24 AM", "True", "True"); }, 1600);
       setTimeout(function () { addThem(4, "img/spixiavatar.png", "And how are you?", "11:24 AM", "True", "True"); }, 1900);
       setTimeout(function () { addFile(12, 12, "", "file.png", "10:27 AM", "True", "True", "True"); }, 2200);
       setTimeout(function () { addMe(5, "", "Great! Just got back from the moon.", "11:24 AM", "True", "True"); }, 2200);
       setTimeout(function () { addThem(6, "img/spixiavatar.png", "Ohh??? What were you doing there?", "11:25 AM", "True", "True"); }, 2500);
       setTimeout(function () { addMe(7, "", "Was building my own Luna Park...", "11:25 AM", "True", "False"); }, 3000);
       setTimeout(function () { addMe(8, "", "html<div>injection</div>test", "11:25 AM", "False", "False"); }, 4000);

       setTimeout(function () { updateMessage(12, "50", "True", "True"); }, 5000);*/
}

document.getElementById("ca_request").onclick = function () {
    hideAttachBar();
    location.href = "ixian:request";
}
document.getElementById("ca_send").onclick = function () {
    hideAttachBar();
    location.href = "ixian:send";
}

document.getElementById("ca_sendfile").onclick = function () {
    hideAttachBar();
    location.href = "ixian:sendfile";
}

document.getElementById("chat_send").onclick = function () {
    hideAttachBar();
    var chatInput = document.getElementById("chat_input");
    chatInput.click();
    chatInput.focus();
    var chat_text = chatInput.innerText;

    if (chat_text.length > 1000) {
        alert(SL_ChatTextTooLong);
        return false;
    }

    if (chat_text.length > 0)
        location.href = "ixian:chat:" + encodeURIComponent(chat_text);
}

var shiftPressed = false;

var lastTypingSent = new Date().getTime();

$("#chat_input").keydown(function (event) {
    if (event.keyCode === 16) {
        shiftPressed = true;
    }
    if (isBot) {
        return;
    }
    if (new Date().getTime() - lastTypingSent > 1000) {
        lastTypingSent = new Date().getTime();
        location.href = "ixian:typing";
    }
});

$("#chat_input").keypress(function (event) {
    if (event.keyCode === 13) {
        if (!shiftPressed) {
            return false;
        }
    }
});

$("#chat_input").keyup(function (event) {
    if (event.keyCode === 16) {
        shiftPressed = false;
    }
    if (event.keyCode === 13 && !shiftPressed) {
        $("#chat_send").click();
    }

    var chat_text = document.getElementById("chat_input").innerHTML;

    if (chat_text.length > 0 && chat_text != "<br>")
        document.getElementById("chat_send").className = "chatbar-sendbutton chatbar-sendbutton-active";
    else
        document.getElementById("chat_send").className = "chatbar-sendbutton";

});

// TODO: check when this doesn't work correctly
function shouldScroll() {
    return true;
}

var chatInput = document.getElementById("chat_input");

function scrollToBottom() {
    if (shouldScroll()) {
        requestAnimationFrame(function () {
            messagesEl.scrollTop = messagesEl.scrollHeight;
        });
    }
}

$("#chat_input").focus(function (event) {
    hideAttachBar();

    scrollToBottom();
    /*
    if (shouldScroll()) {
        setTimeout(function () {			
            //document.getElementById("chatholder").scrollIntoView(false);
            var messagesEl = document.getElementById('messages');
            messagesEl.scrollTop = messagesEl.scrollHeight;
            // Hack for slow devices
            setTimeout(function () {
                //document.getElementById("chatholder").scrollIntoView(false);
                messagesEl.scrollTop = messagesEl.scrollHeight;
            }, 800);
        }, 200);
    }*/
});

chatInput.addEventListener("blur", function () {
    updateChatInputPlaceholder();
});

function updateChatInputPlaceholder() {
    if (chatInput.textContent.trim() === "") {
        chatInput.classList.add("placeholder");
        chatInput.setAttribute("data-placeholder", SL_ChatPlaceholder);
    } else {
        chatInput.classList.remove("placeholder");
        chatInput.removeAttribute("data-placeholder");
    }
}

chatInput.addEventListener("input", updateChatInputPlaceholder);

$("#chat_input").on('paste', function (e) {
    var data = e.clipboardData || window.clipboardData;

    var inputEl = document.getElementById("chat_input");

    var inputElData = inputEl.innerHTML;

    var caret = null;
    try {
        caret = getCaretPosition(inputEl);
    } catch (e) {

    }

    if (caret) {
        inputEl.innerHTML = inputElData.substring(0, caret) + data.getData('Text') + inputElData.substring(caret);
    } else if (caret === 0) {
        inputEl.innerHTML = data.getData('Text') + inputElData.substring(caret);
    } else {
        inputEl.innerHTML = inputElData.substring(caret) + data.getData('Text');
    }

    e.stopPropagation();
    e.preventDefault();
});

function clearInput() {
    document.getElementById("chat_input").innerHTML = "";
    document.getElementById("chat_send").className = "chatbar-sendbutton";
}

let scrollTimeout;

messagesEl.addEventListener("scroll", () => {
    const stickyDateEl = document.getElementById("sticky-date");

    // Show sticky header while scrolling
    if (stickyDateEl) {
        stickyDateEl.style.display = "flex";
    }

    // Clear any existing timeout
    if (scrollTimeout) clearTimeout(scrollTimeout);

    // Set timeout to hide sticky header after 1 second of no scroll
    scrollTimeout = setTimeout(() => {
        if (stickyDateEl) {
            stickyDateEl.style.display = "none";
        }
    }, 1000);

    updateStickyHeader();
});

function addReactions(id, reactions) {
    var msgEl = document.getElementById("msg_" + id);
    if (msgEl == null) {
        return;
    }

    var reactionsEls = msgEl.getElementsByClassName("reactions");
    var reactionsEl = null;
    if (reactionsEls.length == 0) {
        if (reactions == "") {
            return;
        }
        reactionsEl = document.createElement("div");
        reactionsEl.className = "reactions";
        msgEl.appendChild(reactionsEl);
    } else {
        reactionsEl = reactionsEls[0];
    }

    reactionsEl.innerHTML = "";
    var reactionArr = reactions.split(";");
    for (var i = 0; i < reactionArr.length; i++) {
        if (reactionArr[i] == "") {
            continue;
        }
        if (reactionArr[i].indexOf("tip:") == 0) {
            reactionsEl.innerHTML += "<div class=\"reaction\"><img class=\"ixicash-icon\" src=\"img/ixicash.svg\"/>" + reactionArr[i].substring(4) + "</div>";
        } else if (reactionArr[i].indexOf("like:") == 0) {
            reactionsEl.innerHTML += "<div class=\"reaction\"><i class=\"fa fa-heart\"></i>" + reactionArr[i].substring(5) + "</div>";
        }
    }

    if (reactionsEl.innerHTML == "") {
        reactionsEl.parentNode.removeChild(reactionsEl);
    }

    if (messagesEl) {
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }
}

function deleteMessage(id) {
    var msgEl = document.getElementById("msg_" + id);
    if (msgEl == null) {
        return;
    }
    msgEl.parentNode.removeChild(msgEl);
}

function linkify(text) {
    text = text.replace(/((http:\/\/|https:\/\/|ftp:\/\/|www\.)[^'"\,\s]+[^\.])/g, function () {
        if (text.match(/^https:\/\/[A-Za-z0-9]+\.(tenor|giphy)\.com\/[A-Za-z0-9_\/=%\?\-\.\&]+$/)) {
            // Giphy/Tenor image
            return "<img src=\"" + escapeParameter(arguments[0]) + "\"/>";
        }
        var link = arguments[0].trim();
        return "<div class=\"spixi-external-link\" onclick=\"onExternalLink(event, '" + escapeParameter(link) + "')\">" + link + "</div> ";
    });
    return text;
}

function visitLink(url) {
    location.href = "ixian:openLink:" + escapeParameter(url);
    hideModalDialog();
}

function onExternalLink(e, url) {
    var title = SL_Modals["externalLinkTitle"];
    var body = SL_Modals["externalLinkBody"];
    body = body.replace("{0}", "<b>" + url + "</b>");
    var visitButtonHtml = "<div onclick=\"visitLink('" + url + "');\">" + SL_Modals["externalLinkButtonVisit"] + "</div>";
    var cancelBtnHtml = "<div onclick='hideModalDialog();'>" + SL_Modals["cancel"] + "</div>";
    showModalDialog(title, body, cancelBtnHtml, visitButtonHtml);
    e.stopPropagation();
    return false;
}

function parseMessageText(text) {
    try {
        text = linkify(text);
    } catch (e) {
    }
    return text;
}

let lastInsertedDate = null;

function formatDateForDateHeader(date) {
    const options = { year: 'numeric', month: 'short', day: 'numeric' };
    return new Date(date).toLocaleDateString(undefined, options);
}

function formatDateForStickyDate(date){
    const options = { day: '2-digit', month: 'short' };
    return new Date(date).toLocaleDateString(undefined, options);
}

const observerOptions = {
    root: document.getElementById('messages'),
    rootMargin: '0px 0px -90% 0px',
    threshold: 0
};

const stickyHeader = document.getElementById('sticky-date');

const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
        if (entry.isIntersecting  && isScrollable(messagesEl)) {
            const newDate = entry.target.getAttribute('data-date');
            stickyHeader.innerText = formatDateForStickyDate(newDate);
            stickyHeader.style.display = "flex";
        }
    });
}, observerOptions);

function observeDateHeaders() {
    const headers = document.querySelectorAll(".date-header");
    headers.forEach(header => observer.observe(header));
}

let dateObserverInitialized = false;

function observeDateHeadersOnce() {
    if (dateObserverInitialized) return;
    dateObserverInitialized = true;

    setTimeout(() => {
        observeDateHeaders();
        setInitialStickyDate();
    }, 500);
}

function updateStickyHeader() {
    const stickyDateEl = document.getElementById("sticky-date");
    const dateHeaders = document.querySelectorAll(".date-header");

    let currentStickyDate = "";

    for (let i = dateHeaders.length - 1; i >= 0; i--) {
        const header = dateHeaders[i];
        const rect = header.getBoundingClientRect();

        if (rect.top < 60) {
            currentStickyDate = formatDateForStickyDate(header.getAttribute("data-date"));
            stickyDateEl.innerText = currentStickyDate;
            stickyDateEl.style.display = "flex";
            break;
        }
    }

    dateHeaders.forEach(header => {
        if (header.dataset.date === currentStickyDate) {
            header.style.visibility = "hidden";
        } else {
            header.style.visibility = "visible";
        }
    });
}

// TODO optimize this function
function addText(id, address, nick, avatar, text, time, className) {
    text = text.replace(/\n/g, "<br>");

    var textEl = document.createElement('div');
    textEl.className = "text selectable";

    text = parseMessageText(text);
    textEl.innerHTML = text;

    var timeClass = "spixi-timestamp";
    const formattedTimestamp = getHoursMinutes(time);

    // Insert date header if this message has a new day
    const messageDate = new Date(time * 1000);
    const messageDateString = formatDateForDateHeader(messageDate);

    if (messageDateString !== lastInsertedDate) {
        lastInsertedDate = messageDateString;

        const dateHeaderEl = document.createElement("div");
        dateHeaderEl.className = "date-header body-sm";
        dateHeaderEl.setAttribute("data-date", messageDateString);
        dateHeaderEl.innerText = messageDateString;

        messagesEl.appendChild(dateHeaderEl);
    }

    if (getTimeDifference(time) < 3600) {
        timeClass = "spixi-timestamp spixi-rel-ts-active";
    }

    var timeEl = document.createElement('div');
    timeEl.setAttribute("data-timestamp", time);
    timeEl.className = "time selectable " + timeClass;
    timeEl.innerHTML = formattedTimestamp;

    var bubbleContentWrapEl = document.createElement('div');

    if (nick != "") {
        var nickEl = document.createElement('div');
        if (nick == address) {
            nickEl.setAttribute("name", "a_" + address);
        }
        nickEl.setAttribute("nick", nick);
        nickEl.setAttribute("address", address);
        nickEl.className = "nick selectable";
        nickEl.innerHTML = nick;
        bubbleContentWrapEl.appendChild(nickEl);
    }

    bubbleContentWrapEl.appendChild(textEl);
    bubbleContentWrapEl.appendChild(timeEl);
    if (className.includes("spixi-bubble myself")) {
        if (className.includes("sent")) {
            bubbleContentWrapEl.innerHTML += "<i class=\"statusIndicator fas fa-check\"></i>";
        } else if (className.includes("default")) {
            bubbleContentWrapEl.innerHTML += "<i class=\"statusIndicator fas fa-clock\"></i>";
        } else if (className.includes("read")) {
            bubbleContentWrapEl.innerHTML += "<i class=\"statusIndicator fas fa-check-double\"></i>";
        } else {
            bubbleContentWrapEl.innerHTML += "<i class=\"statusIndicator fas fa-check-double\"></i>";
        }
    }
    bubbleContentWrapEl.innerHTML += "<i class=\"statusIndicator paid fas fa-wallet\"></i>";

    var bubbleEl = document.createElement("div");
    bubbleEl.id = "msg_" + id;
    bubbleEl.className = className + "";
    bubbleEl.appendChild(bubbleContentWrapEl);

    if (avatar != "") {

        avatar = avatar.replace(/&#92;/g, '\\');

        var avatarEl = document.createElement('img');
        avatarEl.className = "avatar";
        avatarEl.src = avatar;
        if (nick == "") {
            avatarEl.alt = address;
        }
        var avatarHtml = avatarEl.outerHTML;
        avatarEl.onclick = function (e) {
            if (nick == "") {
                address = userAddress;
                nick = userNick;
            }
            showUserDetails(avatarHtml + " " + nick, address);
            e.preventDefault;
        };

        bubbleEl.appendChild(avatarEl);
    }

    hideUserTyping();

    messagesEl.appendChild(bubbleEl);

    adjustLastestBubbles();

    observeDateHeadersOnce();

    if (document.querySelectorAll(".spixi-bubble").length === 1) {
        setInitialStickyDate();
    }

    setTimeout(() => {
        updateStickyHeader();
    }, 500);

    scrollToBottom();
}

// TODO: optimize and merge this function with addText
function adjustLastestBubbles() {
    var bubbles = messagesEl.querySelectorAll(".spixi-bubble");

    if (bubbles.length < 2) {
        return;
    }

    var currentBubble = bubbles[bubbles.length - 1];
    var previousBubble = bubbles[bubbles.length - 2];

    var isCurrentMyself = currentBubble.classList.contains('myself');
    var isPreviousMyself = previousBubble.classList.contains('myself');

    if (isCurrentMyself === isPreviousMyself) {
        if (!isCurrentMyself) {
            var currentNick = currentBubble.querySelector('.nick');
            var previousNick = previousBubble.querySelector('.nick');

            if (currentNick && previousNick) {
                var currentAddress = currentNick.getAttribute('address');
                var previousAddress = previousNick.getAttribute('address');

                if (currentAddress && previousAddress && currentAddress === previousAddress) {
                    var currentAvatar = currentBubble.querySelector('.avatar');
                    if (currentAvatar) {
                        currentAvatar.remove();
                    }
                    currentNick.textContent = '';
                    previousBubble.style.marginBottom = "2px";
                } else {
                    previousBubble.style.marginBottom = "10px";
                }
            } else {
                var currentAvatar = currentBubble.querySelector('.avatar');
                if (currentAvatar) {
                    currentAvatar.remove();
                }
                previousBubble.style.marginBottom = "2px";
            }
        } else {
            previousBubble.style.marginBottom = "2px";
        }
    } else {
        previousBubble.style.marginBottom = "10px";
    }
}

function addMe(id, address, nick, avatar, text, time, sent, confirmed, read, paid) {
    var additionalClasses = "";
    if (confirmed == "True") {
        additionalClasses = " confirmed";
    } else if (sent == "True") {
        additionalClasses = " sent";
    } else {
        additionalClasses = " default";
    }
    if (read == "True") {
        additionalClasses += " read";
    }
    if (paid == "True") {
        additionalClasses += " paid";
    }
    addText(id, address, nick, avatar, text, time, "spixi-bubble myself" + additionalClasses);
}

function addThem(id, address, nick, avatar, text, time) {
    addText(id, address, nick, avatar, text, time, "spixi-bubble");
}

function addFile(id, address, nick, avatar, fileid, name, time, me, sent, read, progress, complete, paid) {

    var additionalClasses = "";
    if (me == "True") {
        additionalClasses = " myself";
        if (sent == "True") {
            additionalClasses += " sent";
        }
        if (read == "True") {
            additionalClasses += " read";
        }
    }

    var textEl = document.createElement('div');
    textEl.className = "text selectable";
    textEl.innerHTML = name;

    var icon = document.createElement('i');
    icon.className = "fa fa-arrow-down actionicon";

    var iconWrap = document.createElement('div');
    iconWrap.className = "icon-wrap";

    iconWrap.appendChild(icon);

    var linkEl = document.createElement('a');
    linkEl.style.display = "block";
    linkEl.id = "file_" + fileid;

    if (me == "True") {
        linkEl.href = "ixian:openfile:" + fileid;
        icon.className = "fa fa-folder-open actionicon";
        linkEl.appendChild(iconWrap);
        linkEl.appendChild(textEl);
    } else {
        linkEl.href = "ixian:acceptfile:" + fileid;
        linkEl.appendChild(textEl);
        linkEl.appendChild(iconWrap);
    }

    if (paid == "True") {
        additionalClasses += " paid";
    }

    addText(id, address, nick, avatar, linkEl.outerHTML, time, "spixi-bubble file" + additionalClasses);

    if (progress == "100") {
        updateFile(fileid, progress, complete);
    }
}

function addCall(id, message, declined, time) {
    var textEl = document.createElement('div');
    textEl.className = "text selectable";
    textEl.innerHTML = message;

    var timeClass = "spixi-timestamp";
    var relativeTime = getRelativeTime(time);
    if (getTimeDifference(time) < 3600) {
        timeClass = "spixi-timestamp spixi-rel-ts-active";
    }

    var timeEl = document.createElement('div');
    timeEl.setAttribute("data-timestamp", time);
    timeEl.className = "time selectable " + timeClass;
    timeEl.innerHTML = relativeTime;

    var icon = document.createElement('div');
    icon.className = "fa fa-phone icon";

    var bubbleEl = document.getElementById("call_" + id);
    var append = false;
    if (bubbleEl == null) {
        bubbleEl = document.createElement('div');
        append = true;
    } else {
        bubbleEl.innerHTML = "";
    }

    bubbleEl.id = "call_" + id;
    bubbleEl.className = "spixi-callbubble";
    if (declined == "True") {
        bubbleEl.className = "spixi-callbubble spixi-callbubble-declined";
        icon.className = "fa fa-phone-slash icon";
    }

    bubbleEl.appendChild(icon);

    var dataEl = document.createElement('div');
    dataEl.className = "spixi-call-data";
    dataEl.appendChild(textEl);
    dataEl.appendChild(timeEl);

    bubbleEl.appendChild(dataEl);


    if (append) {
        messagesEl.appendChild(bubbleEl);
    }

    scrollToBottom();
}

function updateFile(id, progress, complete) {
    var fileEl = document.getElementById("file_" + id);
    if (fileEl != null) {
        if (complete == "True") {
            fileEl.href = "ixian:openfile:" + id;
        } else {
            fileEl.href = "javascript:void(0)";
        }

        var aEls = fileEl.getElementsByClassName("actionicon");
        if (aEls.length == 0)
            aEls = fileEl.getElementsByClassName("actionprogress");

        if (aEls.length > 0) {
            var aEl = aEls[0];
            if (complete == "True") {
                aEl.className = "fa fa-folder-open actionicon";
                aEl.innerHTML = "";
            } else {
                aEl.className = "actionprogress";
                aEl.innerHTML = progress + "<span class='smaller'>%</span>";
            }
        }
    }
}

function updateMessage(id, message, sent, confirmed, read, paid, errorSending) {
    message = message.replace(/\n/g, "<br>");

    var msgEl = document.getElementById("msg_" + id);
    var isFile = false;
    var isPayment = false;

    if (msgEl != null) {

        var additionalClasses = "";

        if (sent == "True") {
            additionalClasses = " sent";
        }
        if (confirmed == "True") {
            additionalClasses = " confirmed";
        }
        if (read == "True") {
            additionalClasses += " read";
        }

        if (paid == "True") {
            additionalClasses += " paid";
        }

        if (errorSending == "True") {
            additionalClasses += " error-sending";
        }

        if (msgEl.className.indexOf("spixi-payment-request") > -1) {
            additionalClasses += " spixi-payment-request";
            isPayment = true;
        }

        if (msgEl.className.indexOf("file") > -1) {
            additionalClasses += " file";
            isFile = true;
        }

        var statusEls = msgEl.getElementsByClassName("statusIndicator");
        if (statusEls.length > 0) {
            var statusEl = statusEls[0];

            if (msgEl.className.includes("spixi-bubble myself")) {
                if (additionalClasses.includes("sent")) {
                    statusEl.className = "statusIndicator fas fa-check";
                } else if (additionalClasses.includes("default")) {
                    statusEl.className = "statusIndicator fas fa-clock";
                } else if (additionalClasses.includes("read")) {
                    statusEl.className = "statusIndicator fas fa-check-double";
                } else {
                    statusEl.className = "statusIndicator fas fa-check-double";
                }
            }
        }

        msgEl.className = "spixi-bubble myself" + additionalClasses;

        if (isFile == false && isPayment == false) {
            message = parseMessageText(message);
            var textEls = msgEl.getElementsByClassName("text");
            if (textEls.length > 0) {
                var textEl = textEls[0];
                textEl.innerHTML = message;
            }
        }
    }
}

function addPaymentRequest(id, txid, address, nick, avatar, title, amount, status, statusIcon, time, localSender, sent, read, enableView) {
    var message = "<div id=\"tx_" + txid + "\" class=\"txid-el\"><div class=\"title\"><i class=\"fas fa-flag\"></i> " + title + "</div>";
    message += "<div class=\"status\">" + SL_ChatStatus + ":<div class=\"content\">" + status + "<i class=\"fas " + statusIcon + "\"></i></div></div>";
    message += "<div class=\"amount\">" + SL_ChatAmount + ":<div class=\"content\"><img class=\"ixicash-icon\" src=\"img/ixicash.svg\"/><span>" + amount + "</span></div></div>";
    var viewStyle = "display:none;";
    if (enableView == "True") {
        viewStyle = "";
    }
    message += "<div class=\"view\" style=\"" + viewStyle + "\" onclick=\"location.href = 'ixian:viewPayment:" + id + "'\"><i class=\"fas fa-search\"></i> " + SL_ChatView + "</div>";

    var additionalClasses = "";
    if (localSender == "True") {
        additionalClasses = " myself";
        if (sent == "True") {
            additionalClasses += " sent";
        }
        if (read == "True") {
            additionalClasses += " read";
        }
    }

    addText(id, address, nick, avatar, message, time, "spixi-bubble spixi-payment-request" + additionalClasses);
}

function updatePaymentRequestStatus(msgId, txid, status, statusIcon, enableView) {
    var el = document.getElementById("msg_" + msgId);

    if (el == null) {
        return;
    }

    var tmpEls = el.getElementsByClassName("txid-el");
    tmpEls[0].id = "tx_" + txid;

    var statusEls = el.getElementsByClassName("status");
    var statusContentEls = statusEls[0].getElementsByClassName("content");
    statusContentEls[0].innerHTML = status + "<i class=\"fas " + statusIcon + "\"></i>";

    var viewEls = el.getElementsByClassName("view");
    if (enableView == "True") {
        viewEls[0].style.display = "";
    } else {
        viewEls[0].style.display = "none";
    }

}

function updateTransactionStatus(txid, status, statusIcon) {
    var el = document.getElementById("tx_" + txid);

    if (el == null) {
        return;
    }

    var statusEls = el.getElementsByClassName("status");
    var statusContentEls = statusEls[0].getElementsByClassName("content");
    statusContentEls[0].innerHTML = status + "<i class=\"fas " + statusIcon + "\"></i>";
}


function addAppRequest(id, appid, appname, appimage, address, nick, avatar, time, localSender, sent, read, appStatus, appInstallURL) {

    var title = SL_ChatAppInviteSent;
    if (localSender != "True") {
        title = SL_ChatAppInviteReceived;
    }

    var message = "<div id=\"app_" + appid + "\" class=\"txid-el\"><div class=\"title\"><i class=\"fas fa-rocket\"></i> " + title + "</div>";

    message += "<div class=\"content\" style=\"display: flex; align-items: center; gap: 8px;\">";
    if (appimage) {
        message += "<img src=\"" + appimage + "\" alt=\"app icon\" class=\"spixi-bubble-app-image\">";
    }
    message += "<span>" + appname + "</span></div>";


    var viewStyle = "display:none;";
    var appButtonIcon = "<i class=\"fas fa-wand-magic-sparkles\"></i> ";
    if (appStatus == "Missing") {
        appButtonIcon = "<i class=\"fas fa-arrows-down-to-line\"></i> ";
        const encodedURL = encodeURIComponent(appInstallURL);
        message += "<div class=\"new-modal-buttons\"><div class=\"spixi-flat-button label-sm\" onclick=\"location.href = 'ixian:installApp:" + encodedURL + "'\"> " + appButtonIcon + SL_ChatAppGet + " </div>";
    }
    else if (appStatus == "Minimized") {
        message += "<div class=\"new-modal-buttons\"><div class=\"spixi-flat-button label-sm\" onclick=\"location.href = 'ixian:joinApp:" + appid + "'\"> " + appButtonIcon + SL_ChatAppBack + " </div>";
    }
    else {
        message += "<div class=\"new-modal-buttons\"><div class=\"spixi-flat-button label-sm\" onclick=\"location.href = 'ixian:joinApp:" + appid + "'\"> " + appButtonIcon + SL_ChatAppLaunch + " </div>";

    }

    var additionalClasses = "";
    if (localSender == "True") {
        additionalClasses = " myself";
        if (sent == "True") {
            additionalClasses += " sent";
        }
        if (read == "True") {
            additionalClasses += " read";
        }
    }

    addText(id, address, nick, avatar, message, time, "spixi-bubble spixi-payment-request" + additionalClasses);
}		


function setNickname(nick) {
    userNick = nick;
    document.getElementById("title").innerHTML = nick;
}

document.getElementById("undorequest").onclick = function () {
    location.href = "ixian:undorequest";
}

function setOnlineStatus(status) {
    document.getElementById("status").innerHTML = status;
}

function updateGroupChatNicks(address, nick) {
    var nickEls = document.getElementsByName("a_" + address);
    for (var i = 0; i < nickEls.length; i++) {
        var nickEl = nickEls[i];
        nickEl.name = "";
        nickEl.setAttribute("nick", nick);
        nickEl.innerHTML = nick;
    }
}

function addApp(id, name, icon, publisher) {
    const appsContainer = document.getElementById("AppsMenu");
    const itemsContainer = appsContainer.querySelector(".choose-apps-items-container");

    const appBlock = document.createElement("div");
    appBlock.className = "spixi-app-details-header";
    appBlock.onclick = function () {
        location.href = "ixian:app:" + id;
        toggleAnimatedSlider("AppsMenu");
    };
    appBlock.innerHTML = `
        <img data-role="AppIcon" class="spixi-app-details-image" src="${icon}" alt="app-icon" />
        <div class="spixi-app-details-title">
            <span class="label-md s-text-01 appNameItem" data-role="AppName">${name}</span>
            <span class="label-sm s-text-02" data-role="AppPublisher">${publisher}</span>
            <span class="label-xs s-text-success" style="display:none;"><i class="fas fa-lock"></i> Verified</span>
        </div>
    `;

    itemsContainer.appendChild(appBlock);
}

function showCallButton() {
    if (!isBot) {
        document.getElementById("CallButton").style.display = "block";
    }
}

function showContacts(e) {
    var contactsBox = document.getElementById("ContactsBox");
    if (contactsBox != null) {
        document.body.removeChild(contactsBox);
    }
    contactsBox = document.createElement("div");
    contactsBox.id = "ContactsBox";
    contactsBox.className = "container-fluid chat-contacts-box";

    document.body.appendChild(contactsBox);

    e.stopPropagation();

    location.href = "ixian:loadContacts";

    return false;
}

function hideContacts() {
    var contactsBox = document.getElementById("ContactsBox");
    if (contactsBox != null) {
        document.body.removeChild(contactsBox);
    }
}

function addContact(address, nick, avatar, role) {
    var contactsBox = document.getElementById("ContactsBox");
    if (contactsBox == null) {
        return;
    }

    var userTemplate = document.getElementsByClassName("user")[0].innerHTML;

    var childEl = document.createElement("div");
    childEl.innerHTML = userTemplate;
    childEl.onclick = function () {
        showUserDetails(nick, address);
    };

    childEl.getElementsByClassName("avatar")[0].innerHTML = "<img src='" + avatar + "'/>";
    childEl.getElementsByClassName("nick")[0].innerHTML = nick;
    if (role == "") {
        role = "[DEFAULT]";
    }

    contactsBox.appendChild(childEl);
}

function selectChannel(id) {
    location.href = "ixian:selectChannel:" + id;
}

var channelSelectorEl = null;

function displayChannelSelector(e) {
    if (channelSelectorEl != null) {
        channelSelectorEl.parentNode.removeChild(channelSelectorEl);
        channelSelectorEl = null;
        e.stopPropagation();
        return false;
    }
    channelSelectorEl = document.createElement("div");
    channelSelectorEl.className = "spixi-channel-selector";
    channelSelectorEl.innerHTML = "<div class='spixi-channel-selector-sep'></div>";

    document.body.appendChild(channelSelectorEl);

    location.href = "ixian:populateChannelSelector";

    e.stopPropagation();

    return false;
}

function setChannelSelectorStatus(read) {
    if (read == "true") {
        document.getElementsByClassName("spixi-channel-bar")[0].className = "spixi-channel-bar";
    } else {
        document.getElementsByClassName("spixi-channel-bar")[0].className = "spixi-channel-bar unread";
    }
}

function addChannelToSelector(id, name, icon, unread) {
    if (channelSelectorEl == null) {
        return;
    }
    var channelTemplate = document.getElementsByClassName("channel-selector-template")[0].innerHTML;

    var childEl = document.createElement("div");
    childEl.innerHTML = channelTemplate;

    if (unread == "True") {
        name = name + "<div class=\"unread-indicator\"></div>";
    }

    childEl.getElementsByClassName("channel-icon")[0].innerHTML = "<i class='fa " + icon + "'></i>";
    childEl.getElementsByClassName("channel-name")[0].innerHTML = name;
    if (id == selectedChannel) {
        childEl.getElementsByClassName("channel-name")[0].style.fontWeight = "bold";
    }

    childEl.onclick = function (ev) {
        selectChannel(id);
        hideChannelSelector();
    };

    channelSelectorEl.appendChild(childEl);

    if (channelSelectorEl.getElementsByClassName("unread-indicator").length == 0) {
        setChannelSelectorStatus("true");
    } else {
        setChannelSelectorStatus("");
    }
}

function hideChannelSelector() {
    if (channelSelectorEl == null) {
        return;
    }

    channelSelectorEl.parentNode.removeChild(channelSelectorEl);
    channelSelectorEl = null;
}

function clearMessages(showMore) {
    messagesEl.innerHTML = "";

    // add spacer to push messaged to the bottom
    const messagesSpacer = document.createElement('div');
    messagesSpacer.className = "messages-spacer";
    messagesEl.appendChild(messagesSpacer);

    // private messages note always visible as first the item
    const privateMessagesNote = document.createElement('div');
    privateMessagesNote.className = "private-messages-note";
    privateMessagesNote.innerHTML = "<i style='font-size: 12px' class='fa-solid fa-lock'></i> <span class='label-xs'>" + SL_ChatPrivate["title"] + "</span> <span class='body-xs'>" + SL_ChatPrivate["message"] + "</span>";
    messagesEl.appendChild(privateMessagesNote);

    if (showMore == "true") {
        const loadMoreDiv = document.createElement('div');
        loadMoreDiv.id = "load_more";
        loadMoreDiv.className = "spixi-flat-button label-sm button outline";
        loadMoreDiv.style.margin = "12px auto";
        loadMoreDiv.innerHTML = "<i class='fa-solid fa-arrow-rotate-right'></i> Show older messages";
        messagesEl.appendChild(loadMoreDiv);
        document.getElementById("load_more").onclick = function () {
            location.href = "ixian:loadmore";
        }
    }
}

function clearApps(showMore) {
    const appsContainer = document.getElementById("AppsMenu");
    const itemsContainer = appsContainer.querySelector(".choose-apps-items-container");

    itemsContainer.innerHTML = "";
}


function displayContextMenu(e) {
    var contextMenuEl = document.getElementById("ContextMenu");
    if (contextMenuEl != null) {
        contextMenuEl.parentNode.removeChild(contextMenuEl);
    }


    var msgEl = null;
    for (var tmpEl = e.target; tmpEl != messagesEl; tmpEl = tmpEl.parentNode) {
        msgEl = tmpEl;
    }

    if (msgEl == null) {
        return false;
    }

    var localMsg = false;
    if (msgEl.className.indexOf("myself") != -1) {
        localMsg = true;
    }

    var menuHtml = "";
    //menuHtml += "<div onclick=\"contextAction('pin', '" +  msgEl.id + "');\"><span class=\"icon\"><i class=\"fa fa-map-pin\"></i></span> " + SL_ContextMenu["pinMessage"] + "</div>";
    menuHtml += "<div onclick=\"contextAction('copy', '" + msgEl.id + "');\"><span class=\"icon\"><i class=\"fa fa-quote-right\"></i></span> " + SL_ContextMenu["copyMessage"] + "</div>";
    menuHtml += "<div onclick=\"contextAction('copySelected', '" + msgEl.id + "');\"><span class=\"icon\"><i class=\"fa fa-copy\"></i></span> " + SL_ContextMenu["copySelected"] + "</div>";
    if (!localMsg) {
        menuHtml += "<div onclick=\"contextAction('tip', '" + msgEl.id + "');\"><span class=\"icon\"><i class=\"fa fa-wallet\"></i></span> " + SL_ContextMenu["tipUser"] + "</div>";
        menuHtml += "<div onclick=\"contextAction('like', '" + msgEl.id + "');\"><span class=\"icon\"><i class=\"fa fa-heart\"></i></span> " + SL_ContextMenu["likeMessage"] + "</div>";
        if (isBot) {
            menuHtml += "<div onclick=\"contextAction('userInfo', '" + msgEl.id + "');\"><span class=\"icon\"><i class=\"fa fa-info-circle\"></i></span> " + SL_ContextMenu["userInfo"] + "</div>";
            menuHtml += "<div onclick=\"contextAction('sendContactRequest', '" + msgEl.id + "');\"><span class=\"icon\"><i class=\"fa fa-user-plus\"></i></span> " + SL_ContextMenu["sendContactRequest"] + "</div>";
            menuHtml += "<div onclick=\"contextAction('report', '" + msgEl.id + "');\"><span class=\"icon\"><i class=\"fa fa-flag\"></i></span> " + SL_ContextMenu["reportMessage"] + "</div>";

        }
    }

    if (isAdmin) {
        menuHtml += "<div onclick=\"contextAction('kickUser', '" + msgEl.id + "');\"><span class=\"icon\"><i class=\"fa fa-user-times\"></i></span> " + SL_ContextMenu["kickUser"] + "</div>";
        menuHtml += "<div onclick=\"contextAction('banUser', '" + msgEl.id + "');\"><span class=\"icon\"><i class=\"fa fa-user-slash\"></i></span> " + SL_ContextMenu["banUser"] + "</div>";
    }
    if (isAdmin || localMsg || !isBot) {
        menuHtml += "<div onclick=\"contextAction('deleteMessage', '" + msgEl.id + "');\"><span class=\"icon\"><i class=\"fa fa-trash-alt\"></i></span> " + SL_ContextMenu["deleteMessage"] + "</div>";
    }

    contextMenuEl = document.createElement("div");
    contextMenuEl.id = "ContextMenu";
    contextMenuEl.className = "chat-context-menu";
    contextMenuEl.onclick = function (e) {
        e.stopPropagation();
        return false;
    };
    contextMenuEl.onmousedown = function (e) {
        e.stopPropagation();
        e.preventDefault();
        return false;
    };

    contextMenuEl.innerHTML = menuHtml;
    contextMenuEl.style.left = "0px";
    contextMenuEl.style.top = e.clientY + "px";
    contextMenuEl.style.bottom = "auto";
    contextMenuEl.style.right = "auto";

    document.body.appendChild(contextMenuEl);


    if (contextMenuEl.getBoundingClientRect().bottom > window.innerHeight) {
        contextMenuEl.style.top = "auto";
        contextMenuEl.style.bottom = "0px";
        contextMenuEl.style.maxHeight = "350px";
    }

    var menuWidth = contextMenuEl.offsetWidth;

    if (e.clientX + menuWidth > window.innerWidth) {
        contextMenuEl.style.left = "auto";
        contextMenuEl.style.right = "0px";
        contextMenuEl.style.minWidth = menuWidth + "px";
    } else {
        contextMenuEl.style.left = e.clientX + "px";
    }
    document.addEventListener('click', handleContextMenuOutsideClick, true);
    return true;
}

function handleContextMenuOutsideClick(event) {
    const contextMenuEl = document.getElementById('ContextMenu');
    if (contextMenuEl && !contextMenuEl.contains(event.target)) {
        hideContextMenus();
    }
}

function hideContextMenu() {
    var contextMenuEl = document.getElementById("ContextMenu");
    if (contextMenuEl != null) {
        document.removeEventListener('click', handleContextMenuOutsideClick, true);
        contextMenuEl.parentNode.removeChild(contextMenuEl);
    }
}

var contextActionMsgId = null;
var tipPrice = "0";

function contextAction(action, msgId) {
    msgId = msgId.substring(4);
    contextActionMsgId = msgId;
    if (action == "copy") {
        window.getSelection().selectAllChildren(document.getElementById("msg_" + msgId).getElementsByClassName("text")[0]);
        document.execCommand('copy');
        window.getSelection().removeAllRanges();
    } else if (action == "copySelected") {
        document.execCommand('copy');
    } else if (action == "tip") {
        var msgEl = document.getElementById("msg_" + msgId);
        var address = null;
        var nick = null;
        if (msgEl.getElementsByClassName("nick").length > 0) {
            address = msgEl.getElementsByClassName("nick")[0].getAttribute("address");
            nick = msgEl.getElementsByClassName("nick")[0].getAttribute("nick");
        }
        if (address == null) {
            address = userAddress;
        }
        if (nick == null) {
            nick = userNick;
        }

        var title = SL_Modals["tipTitle"];
        title = title.replace("{0}", nick);

        tipPrice = "0";
        var html = SL_Modals["tipBody"];

        html += "<div class=\"spixi-modal-tip-item\" onclick=\"selectTip('50');\">50 IXI <i class=\"fa fa-check\"></i></div>";
        html += "<div class=\"spixi-modal-tip-item\" onclick=\"selectTip('100');\">100 IXI <i class=\"fa fa-check\"></i></div>";
        html += "<div class=\"spixi-modal-tip-item\" onclick=\"selectTip('200');\">200 IXI <i class=\"fa fa-check\"></i></div>";
        html += "<div class=\"spixi-modal-tip-item custom\" onclick=\"selectTip();\">" + SL_Modals["tipCustom"] + " <i class=\"fa fa-check\"></i> <input type='text' class='spixi-textfield' onchange='tipPrice = this.value;'/></div>";

        var payBtnHtml = "<div onclick='payTipConfirmation(\"" + msgId + "\");'>" + SL_Modals["payButton"] + "</div>";
        var cancelBtnHtml = "<div onclick='hideModalDialog();'>" + SL_Modals["cancel"] + "</div>";

        showModalDialog(title, html, payBtnHtml, cancelBtnHtml);
    } else if (action == "userInfo") {
        var msgEl = document.getElementById("msg_" + msgId);
        var nick = msgEl.getElementsByClassName("nick")[0].getAttribute("nick");
        var address = msgEl.getElementsByClassName("nick")[0].getAttribute("address");
        var avatar = msgEl.getElementsByClassName("avatar")[0].outerHTML;
        showUserDetails(avatar + " " + nick, address);
    } else if (action == "report") {
        showReportModal(msgId);
    } else {
        location.href = "ixian:contextAction:" + action + ":" + msgId;
    }
    hideContextMenu();
}

function selectTip(amount) {
    var modalEl = document.getElementById("SpixiModalDialog");

    var tipItems = modalEl.getElementsByClassName("spixi-modal-tip-item");

    tipItems[0].className = tipItems[1].className = tipItems[2].className = tipItems[3].className = "spixi-modal-tip-item";

    if (amount == "50") {
        tipItems[0].className += " selected";
        tipPrice = amount;
        modalEl.getElementsByClassName("spixi-textfield")[0].value = "";
    } else if (amount == "100") {
        tipItems[1].className += " selected";
        tipPrice = amount;
        modalEl.getElementsByClassName("spixi-textfield")[0].value = "";
    } else if (amount == "200") {
        tipItems[2].className += " selected";
        tipPrice = amount;
        modalEl.getElementsByClassName("spixi-textfield")[0].value = "";
    } else {
        tipItems[3].className += " selected";
        if (modalEl.getElementsByClassName("spixi-textfield")[0].value != "") {
            tipPrice = modalEl.getElementsByClassName("spixi-textfield")[0].value;
        } else {
            tipPrice = "0";
        }
    }
}

function payTipConfirmation(msgId) {
    if (tipPrice == "0") {
        return;
    }

    var msgEl = document.getElementById("msg_" + msgId);
    var address = null;
    var nick = null;
    if (msgEl.getElementsByClassName("nick").length > 0) {
        address = msgEl.getElementsByClassName("nick")[0].getAttribute("address");
        nick = msgEl.getElementsByClassName("nick")[0].getAttribute("nick");
    }
    if (address == null) {
        address = userAddress;
    }
    if (nick == null) {
        nick = userNick;
    }

    var title = SL_Modals["tipTitle"];
    title = title.replace("{0}", nick);

    var html = SL_Modals["tipConfirmationBody"];

    html = html.replace("{0}", nick + " (" + address + ")");
    html = html.replace("{1}", tipPrice + " IXI");

    var payBtnHtml = "<div onclick='payTip();'>" + SL_Modals["payButton"] + "</div>";
    var cancelBtnHtml = "<div onclick='hideModalDialog();'>" + SL_Modals["cancel"] + "</div>";

    showModalDialog(title, html, payBtnHtml, cancelBtnHtml);
}

function payTip() {
    hideModalDialog();
    location.href = "ixian:contextAction:tip:" + contextActionMsgId + ":" + tipPrice;
}

function showBotDetails() {
    if (!isBot) {
        location.href = "ixian:details";
        return;
    }
    document.getElementById("BotDetails").style.display = "block";
}

function hideBotDetails() {
    document.getElementById("BotDetails").style.display = "none";
}

function toggleNotifications(el) {
    if (notifications) {
        notifications = false;
        el.className = "spixi-switch off spixi-bot-notifications-toggle";
        location.href = "ixian:disableNotifications";
    } else {
        notifications = true;
        el.className = "spixi-switch spixi-bot-notifications-toggle";
        location.href = "ixian:enableNotifications";
    }
}

var clipboardJs = new ClipboardJS('.address_qr_holder');

clipboardJs.on('success', function (e) {
    e.clearSelection();

    var x = document.getElementsByClassName("spixi-toastbar")[0];
    x.className = "spixi-toastbar show";
    setTimeout(function () {
        x.className = x.className.replace("show", "");
    }, 3000);

});

clipboardJs.on('error', function (e) {

});

var botQrCode = new QRCode("BotQrCode", {
    text: "",
    width: 200,
    height: 200,
    colorDark: "#000000",
    colorLight: "#ffffff",
    correctLevel: QRCode.CorrectLevel.H
});

var userQrCode = new QRCode("UserQrCode", {
    text: "",
    width: 200,
    height: 200,
    colorDark: "#000000",
    colorLight: "#ffffff",
    correctLevel: QRCode.CorrectLevel.H
});

function setBotAddress(addr) {
    bot_address = addr;
    var parts = addr.match(/.{1,17}/g) || [];
    document.getElementById("BotWal1").innerHTML = parts[0];
    document.getElementById("BotWal2").innerHTML = parts[1];
    document.getElementById("BotWal3").innerHTML = parts[2];
    document.getElementById("BotWal4").innerHTML = parts[3];
    document.getElementById("BotAddressQrHolder").setAttribute("data-clipboard-text", addr);
    botQrCode.clear(); // clear the code.
    botQrCode.makeCode(addr);
}

function setUserAddress(addr) {
    user_address = addr;
    var parts = addr.match(/.{1,17}/g) || [];
    document.getElementById("UserWal1").innerHTML = parts[0];
    document.getElementById("UserWal2").innerHTML = parts[1];
    document.getElementById("UserWal3").innerHTML = parts[2];
    document.getElementById("UserWal4").innerHTML = parts[3];
    document.getElementById("UserAddressQrHolder").setAttribute("data-clipboard-text", addr);
    userQrCode.clear(); // clear the code.
    userQrCode.makeCode(addr);
}

function toggleSpixiBotAddress(toggleEl, addressElId) {
    var addressEl = document.getElementById(addressElId);
    if (addressEl.style.display == "none") {
        addressEl.style.display = "block";
        toggleEl.className = "fa fa-chevron-up";
    } else {
        addressEl.style.display = "none";
        toggleEl.className = "fa fa-chevron-down";
    }
}

function showUserDetails(nick, address) {
    var userDetailsEl = document.getElementById("UserDetails");
    userDetailsEl.style.display = "block";
    userDetailsEl.getElementsByClassName("spixi-bot-user-nick")[0].innerHTML = nick;
    setUserAddress(address);
}

function hideUserDetails(e) {
    if (e.target != e.currentTarget) {
        return;
    }
    var userDetailsEl = document.getElementById("UserDetails");
    userDetailsEl.style.display = "none";
}

function sendContactRequest(address) {
    location.href = "ixian:sendContactRequest:" + address;
}

function kickUser() {
    var address = document.getElementById('UserAddressQrHolder').getAttribute('data-clipboard-text');

    var title = SL_Modals["kickTitle"];
    title = title.replace("{0}", address);

    var html = SL_Modals["kickBody"];
    html = html.replace("{0}", address);

    var payBtnHtml = "<div onclick=\"location.href='ixian:kick:" + address + "';\">" + SL_Modals["kickButton"] + "</div>";
    var cancelBtnHtml = "<div onclick='hideModalDialog();'>" + SL_Modals["cancel"] + "</div>";

    showModalDialog(title, html, payBtnHtml, cancelBtnHtml);
}

function banUser() {
    var address = document.getElementById('UserAddressQrHolder').getAttribute('data-clipboard-text');

    var title = SL_Modals["banTitle"];
    title = title.replace("{0}", address);

    var html = SL_Modals["banBody"];
    html = html.replace("{0}", address);

    var payBtnHtml = "<div onclick=\"location.href='ixian:ban:" + address + "';\">" + SL_Modals["banButton"] + "</div>";
    var cancelBtnHtml = "<div onclick='hideModalDialog();'>" + SL_Modals["cancel"] + "</div>";

    showModalDialog(title, html, payBtnHtml, cancelBtnHtml);
}

var userTypingTimeout = null;

function showUserTyping() {
    if (userTypingTimeout != null) {
        clearTimeout(userTypingTimeout);
        userTypingTimeout = null;
    }
    var userTypingEl = document.getElementById("UserTyping");
    userTypingEl.style.visibility = "visible";
    userTypingTimeout = setTimeout(hideUserTyping, 5000);
}

function hideUserTyping() {
    document.getElementById("UserTyping").style.visibility = "";
    if (userTypingTimeout != null) {
        clearTimeout(userTypingTimeout);
        userTypingTimeout = null;
    }
}

function setUnreadIndicator(unread_count) {
    if (unread_count != "0") {
        document.getElementById("backbtn").className = "unread";
    } else {
        document.getElementById("backbtn").className = "";
    }
}

function showRequestSentModal(show) {
    if (show == true) {
        requestSentModal.style.display = "block";
        document.getElementById("chatbar").style.display = "none";
        document.getElementById("CallButton").style.display = "none";
        document.getElementById("chat_input").disabled = true;
    } else {
        requestSentModal.style.display = "none";
        document.getElementById("chatbar").style.display = "block";
        document.getElementById("CallButton").style.display = "block";
        document.getElementById("chat_input").disabled = false;
    }
}

function showReportModal(msgId) {
    hideContextMenus();
    reportModal.style.display = "block";
    reportMsgId = msgId;
    document.getElementById("chatbar").style.display = "none";
    document.getElementById("chat_input").disabled = true;
}

function hideReportModal() {
    reportModal.style.display = "none";
    document.getElementById("chatbar").style.display = "block";
    document.getElementById("chat_input").disabled = false;
}

function reportMessage() {
    location.href = "ixian:contextAction:report:" + reportMsgId;
    hideReportModal();
}

document.getElementById("request_bar_ignore").onclick = function () {
    location.href = "ixian:undorequest";
}
document.getElementById("request_bar_accept").onclick = function () {
    showContactRequest(false);
    location.href = "ixian:accept";
}

function showContactRequest(show) {
    if (show == true) {
        requestReceivedModal.style.display = "block";
        document.getElementById("chatbar").style.display = "none";
        document.getElementById("CallButton").style.display = "none";
        document.getElementById("chat_input").disabled = true;
    } else {
        requestReceivedModal.style.display = "none";
        document.getElementById("chatbar").style.display = "block";
        document.getElementById("CallButton").style.display = "block";
        document.getElementById("chat_input").disabled = false;
    }
}

function searchMiniApps() {
    const searchValue = document.getElementById("searchMiniApps")?.value.toLowerCase();
    const appItems = document.querySelectorAll("#AppsMenu .spixi-app-details-header");

    appItems.forEach(item => {
        const nameElement = item.querySelector(".appNameItem");
        const appName = nameElement ? nameElement.textContent.toLowerCase() : "";

        if (appName.includes(searchValue)) {
            item.style.display = "flex";
        } else {
            item.style.display = "none";
        }
    });
}

// function getCaretPosition copied from https://stackoverflow.com/questions/3972014/get-contenteditable-caret-index-position
function getCaretPosition(editableDiv) {
    var caretPos = 0,
        sel, range;
    if (window.getSelection) {
        sel = window.getSelection();
        if (sel.rangeCount) {
            range = sel.getRangeAt(0);
            if (range.commonAncestorContainer.parentNode == editableDiv) {
                caretPos = range.endOffset;
            }
        }
    } else if (document.selection && document.selection.createRange) {
        range = document.selection.createRange();
        if (range.parentElement() == editableDiv) {
            var tempEl = document.createElement("span");
            editableDiv.insertBefore(tempEl, editableDiv.firstChild);
            var tempRange = range.duplicate();
            tempRange.moveToElementText(tempEl);
            tempRange.setEndPoint("EndToEnd", range);
            caretPos = tempRange.text.length;
        }
    }
    return caretPos;
}

// Fix for iOS toolbar offscreen issue when soft keyboard is shown
const initialOffset = window.outerHeight - window.innerHeight;
let updatedOffset = initialOffset;
let msgHeight = messagesEl.style.height || (window.innerHeight - 120) + "px";

function iosFixer(overshoot = 0) {
    const newOffset = window.outerHeight - window.innerHeight;

    if (newOffset >= updatedOffset) {
        const diff = newOffset - initialOffset;
        requestAnimationFrame(function () {
            //wrapEl.style.transition = "top 0.20s ease-out";
            //messagesEl.style.transition = "height 0.20s ease-out";

            wrapEl.style.top = `${diff + overshoot}px`;
            messagesEl.style.height = `${window.innerHeight - overshoot - 120}px`;
            debouncedScrollToBottom();
        });
    } else if (newOffset < updatedOffset && wrapEl.style.top != "0px") {
        requestAnimationFrame(function () {
            //wrapEl.style.transition = "top 0.20s ease-out";
            //messagesEl.style.transition = "height 0.20s ease-out";

            wrapEl.style.top = "0px";
            messagesEl.style.height = msgHeight;
            debouncedScrollToBottom();
        });
    }
    updatedOffset = newOffset;
}

const throttle = (fn, delay) => {
    let lastCall = 0;
    return (...args) => {
        const now = Date.now();
        if (now - lastCall >= delay) {
            lastCall = now;
            fn(...args);
        }
    };
};

const debounce = (fn, delay) => {
    let timeout;
    return (...args) => {
        clearTimeout(timeout);
        timeout = setTimeout(() => fn(...args), delay);
    };
};

const debouncedScrollToBottom = debounce(scrollToBottom, 200);
const debouncedIOSFixer = debounce(iosFixer, 25);

// Mobile only logic
if (SL_Platform != "Xamarin-WPF") {
    if (SL_Platform == "Xamarin-iOS") {
        window.visualViewport.addEventListener('resize', () => {
            debouncedIOSFixer();
            debouncedScrollToBottom();
        });
    } else {
        window.addEventListener('resize', function () {
            debouncedScrollToBottom();
        });
    }
}