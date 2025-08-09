// SPIXI helper script

// Disable drag
document.addEventListener('dragstart', (event) => {
    event.preventDefault();
});

function onload()
{
    location.href = "ixian:onload";
    startRelativeTimeUpdate("spixi-rel-ts-active");
}

function isBlank(str) {
    return (!str || /^\s*$/.test(str));
}

function base64ToBytes(base64) {
    const binString = atob(base64);
    return new TextDecoder().decode(Uint8Array.from(binString, (m) => m.codePointAt(0)));
}

function executeUiCommand(cmd) {
    try {
        var decodedArgs = new Array();
        for (var i = 1; i < arguments.length; i++) {
            decodedArgs.push(escapeParameter(base64ToBytes(arguments[i])));
        }
        cmd.apply(null, decodedArgs);
    } catch (e) {
        var alertMessage = "Arguments: " + decodedArgs.join(", ") + "\nError: " + e + "\nStack: " + e.stack; alert(alertMessage);
    }
}

function unescapeParameter(str)
{
    return str.replace(/&gt;/g, ">")
            .replace(/&lt;/g, "<")
            .replace(/&#92;/g, "\\")
            .replace(/&#39;/g, "'")
            .replace(/&#34;/g, "\"");
}

function escapeParameter(str)
{
   return str.replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
}

function quickScanJS() {
    let scanner = new Instascan.Scanner({});
    scanner.addListener('scan', function (content) {
        location.href = "ixian:qrresult:" + content;
    });
    Instascan.Camera.getCameras().then(function (cameras) {
        if (cameras.length > 0) {
            scanner.start(cameras[0]);
        } else {
            console.error('No cameras found.');
            alert("No cameras found.");
        }
    }).catch(function (e) {
        console.error(e);
        alert("Cannot connect to camera.");
    });
}

function amountWithCommas(n) {
    var parts = n.split(".");
    return parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ",") + (parts[1] ? "." + parts[1] : "");
}

function getTimeDifference(unixTimestamp)
{
    var curTime = Date.now();
    var delta = Math.floor(curTime / 1000) - unixTimestamp;
    return delta;
}

function getRelativeTime(unixTimestamp)
{
    var delta = getTimeDifference(unixTimestamp);

    if (delta < 30)
        return SL_TimeJustNow;

    if (delta < 90)
        return SL_AMinuteAgo;

    if (delta < 3600)
        return Math.floor(delta / 60) + " " + SL_MinutesAgo;

    var fullDate = new Date(unixTimestamp * 1000);
    return fullDate.toLocaleString();
}

var relativeTimeUpdateinterval = null;

function startRelativeTimeUpdate(className) {
    if (relativeTimeUpdateinterval != null) return;

    relativeTimeUpdateinterval = setInterval(function () {
        try {
            const els = document.getElementsByClassName(className);
            if(els.length === 0)
            {
                clearInterval(relativeTimeUpdateinterval);
                relativeTimeUpdateinterval = null;
			}
            for (let el of els) {
                const ts = parseInt(el.getAttribute("data-timestamp"), 10);
                if (!isNaN(ts)) {
                    el.innerHTML = getUserFriendlyFormattedTimestamp(ts);
                }
            }
        } catch (e) {
        }
    }, 30000);
}

var callTimeUpdateinterval = null;

function startCallTimeUpdate(className)
{
    if (callTimeUpdateinterval != null) {
        return;
    }

    callTimeUpdateinterval = setInterval(function () {
        try {
            var els = document.getElementsByClassName(className);
            for (var i = 0; i < els.length; i++) {
                var el = els[i];
                var totalTime = Math.floor((Date.now() - el.getAttribute("data-start-timestamp")) / 1000);
                var mins = Math.floor(totalTime / 60);
                var secs = Math.floor(totalTime % 60);
                if(secs < 10)
                {
                    secs = "0" + secs;
				}
                el.innerHTML = mins + ":" + secs;
            }
        } catch (e) {
        }
    }, 500);
}

function addCallAppRequest(senderAddress, sessionId, text) {
    removeAppRequest(sessionId);

    var el = document.createElement("div");
    el.id = "AppReq_" + sessionId;
    el.className = "spixi-callbar";

    var acceptAction = "appAccept('" + senderAddress + "', '" + sessionId + "');";
    var rejectAction = "appReject('" + senderAddress + "', '" + sessionId + "');";

    var acceptHtml = "<div style='background:#2fd63b;border-radius:16px;width:64px;height:64px;display:table-cell;vertical-align:middle;text-align:center;'><i class='fas fa-phone'></i></div>";
    var rejectHtml = "<div style='background:#de0a61;border-radius:16px;width:64px;height:64px;display:table-cell;vertical-align:middle;text-align:center;'><i class='fas fa-phone-slash'></i></div>";

    el.innerHTML = '<div class="spixi-callbar-title">' + text + '</div><div class="spixi-callbar-separator"></div><div class="row spixi-callbar-actions"><div class="col-6"><div onclick="' + acceptAction + '" style="display:inline-block;">' + acceptHtml + '</div></div><div class="col-6" style="text-align:right;"><div onclick="' + rejectAction + '" style="display:inline-block;">' + rejectHtml + '</div></div></div>';

    document.body.appendChild(el);
}

function addAppRequest(senderAddress, sessionId, text, acceptHtml, rejectHtml) {
    removeAppRequest(sessionId);

/*  // TODO: Uncomment this when global app request support is implemented
    var el = document.createElement("div");
    el.id = "AppReq_" + sessionId;
    el.className = "spixi-callbar";

    var acceptAction = "appAccept('" + senderAddress + "', '" + sessionId + "');";
    var rejectAction = "appReject('" + senderAddress + "', '" + sessionId + "');";

    acceptHtml = unescapeParameter(acceptHtml);
    rejectHtml = unescapeParameter(rejectHtml);

    el.innerHTML = '<div class="spixi-callbar-title">' + text + '</div><div class="spixi-callbar-separator"></div><div class="row spixi-callbar-actions"><div class="col-6"><div onclick="' + acceptAction + '" style="display:inline-block;">' + acceptHtml + '</div></div><div class="col-6" style="text-align:right;"><div onclick="' + rejectAction + '" style="display:inline-block;">' + rejectHtml + '</div></div></div>';

    document.body.appendChild(el);*/
}

function removeAppRequest(sessionId) {
    var el = document.getElementById("AppReq_" + sessionId);
    if (el != null) {
        el.parentElement.removeChild(el);
    }
}

function clearAppRequests() {
    var els = document.getElementsByClassName("spixi-callbar");
    for (var i = 0; i < els.length; i++) {
        var el = els[i];
        el.parentElement.removeChild(el);
    }
}

function appAccept(senderAddress, sessionId) {
    var el = document.getElementById("AppReq_" + sessionId);
    el.parentElement.removeChild(el);
    location.href = 'ixian:appAccept:' + senderAddress + ':' + sessionId;
}

function appReject(senderAddress, sessionId) {
    var el = document.getElementById("AppReq_" + sessionId);
    el.parentElement.removeChild(el);
    location.href = 'ixian:appReject:' + senderAddress + ':' + sessionId;
}

function displayCallBar(sessionId, text, displayTime)
{
    var el = document.getElementById("CallBar");
    if(el == null)
    {
        el = document.createElement("div");
        document.body.appendChild(el);
    }else
    {
        el.style.display = "block";
	}
    el.id = "CallBar";
    el.className = "spixi-callbar";

    var rejectAction = "hangUp('" + sessionId + "');";

    var timeHtml = "";
    if(displayTime != "0")
    {
        timeHtml = '<div class="spixi-callbar-duration" data-start-timestamp="' + (displayTime * 1000) + '"></div>';
    }
    hangUpHtml = "<div style='background:#de0a61;border-radius:16px;width:64px;height:64px;display:table-cell;vertical-align:middle;text-align:center;'><i class='fas fa-phone-slash'></i></div>";
    el.innerHTML = '<div class="spixi-callbar-title">' + text + '</div><div class="spixi-callbar-separator"></div><div class="row spixi-callbar-actions"><div class="col-6">' + timeHtml + '</div><div class="col-6" style="text-align:right;"><div onclick="' + rejectAction + '" style="display:inline-block;">' + hangUpHtml + '</div></div></div>';
    if(displayTime != "0")
    {
        startCallTimeUpdate("spixi-callbar-duration");
    }
}

function hangUp(sessionId)
{
    location.href = 'ixian:hangUp:' + sessionId;
}

function hideCallBar()
{
    var callBarElement = document.getElementById("CallBar");
    if (callBarElement) {
        callBarElement.style.display = "none";
    }
}

function showWarning(text) {
    var el = document.getElementById("warning_bar");
    if(el == null)
    {
         return;
	}
    if (text == "") {
        el.style.display = 'none';
    }
    else {
        el.style.display = 'block';
        var msgEls = el.getElementsByClassName("spixi-errorbar-message");
        msgEls[0].innerHTML = text;
    }
}

var modalHtml = '<div class="modal-content" onclick="event.stopPropagation(); return false;">\
                <div class="spixi-modal-header warn">\
                </div>\
                <hr class="spixi-separator noheightmargins fullwidth" />\
                \
                <div class="spixi-modal-text">\
                </div>\
                \
                <hr class="spixi-separator noheightmargins fullwidth" />\
                <div class="spixi-modal-footer">\
                    <div class="spixi-modal-button-left"></div>\
                    <div class="spixi-modal-button-right"></div>\
                </div>\
        </div>';

function showModalDialog(title, body, leftButton, rightButton){
    hideModalDialog();

    var modalEl = document.createElement("div");
    modalEl.id = "SpixiModalDialog";
    modalEl.className = "spixi-modal";
    modalEl.innerHTML = modalHtml;
    modalEl.onclick = hideModalDialog;

    modalEl.getElementsByClassName("spixi-modal-header")[0].innerHTML = title;
    modalEl.getElementsByClassName("spixi-modal-text")[0].innerHTML = body;

    modalEl.getElementsByClassName("spixi-modal-button-left")[0].innerHTML = leftButton;
    modalEl.getElementsByClassName("spixi-modal-button-right")[0].innerHTML = rightButton;

    document.body.appendChild(modalEl);
    modalEl.style.display = "block";
}

function hideModalDialog()
{
    var modalEl = document.getElementById("SpixiModalDialog");
    if(modalEl != null)
    {
        document.body.removeChild(modalEl);
	}
}

function parseBoolean(value) {
    return String(value).toLowerCase() === "true";
}

function toggleAnimatedSlider(elementId) {
    const slideUpContainer = document.getElementById(elementId);
    const toolbarEl = document.getElementById("toolbar");

    if (slideUpContainer.classList.contains('active')) {
        slideUpContainer.classList.remove('active');
        document.getElementById("wrap").classList.remove("blurredContent");
        if (toolbarEl != null) {
            toolbarEl.classList.remove("blurredContent");
        }
    } else {
        slideUpContainer.classList.add('active');
        document.getElementById("wrap").classList.add("blurredContent");
        if (toolbarEl != null) {
            toolbarEl.classList.add("blurredContent");
        }
    }
}

function getHoursMinutes(timestamp) {
    const date = new Date(timestamp * 1000);
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');

    return `${hours}:${minutes}`;
}

function getUserFriendlyFormattedTimestamp(unixTimestamp) {
    const now = new Date();
    const date = new Date(unixTimestamp * 1000);

    const timeDiff = now.getTime() - date.getTime();
    const oneDay = 24 * 60 * 60 * 1000;
    const sevenDays = 7 * oneDay;

    const isSameYear = now.getFullYear() === date.getFullYear();

    if (timeDiff < oneDay) {
        return getHoursMinutes(unixTimestamp);
    } else if (timeDiff < sevenDays || isSameYear) {
        return date.toLocaleString('en-US', { month: 'short', day: 'numeric' });
    } else {
        return date.toLocaleString('en-US', { month: 'short', day: 'numeric', year: "numeric" });
    }
}

function maskWalletAddress(address) {
    if (!address || address.length <= 8) return "";
    return `${address.slice(0, 4)}...${address.slice(-4)}`;
}

function limitToTwoDecimals(inputAsString) {
    const cleaned = inputAsString.trim().replace(/^([+-])\s*/, '$1');
    const parsedNum = parseFloat(cleaned);
    if (isNaN(parsedNum)) return parsedNum;
    return parsedNum.toFixed(2);
}

function checkThemePath(path) {
    const theme = localStorage.getItem('theme') || 'light';
    if (theme === 'dark') {
        return path.replace('img/', 'img/dark/');
    }
    return path;
}

function applyThemeOnDataThemeSrcImages(){
    document.addEventListener('DOMContentLoaded', () => {
        const images = document.querySelectorAll('img[data-theme-src]');
        images.forEach(img => {
            const originalSrc = img.getAttribute('data-theme-src');
            if (originalSrc) {
                img.src = checkThemePath(originalSrc);
            }
        });
    })
}