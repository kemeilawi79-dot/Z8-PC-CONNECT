package com.z8pcconnect.app

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.os.Bundle
import androidx.activity.compose.setContent
import androidx.compose.foundation.Image
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.gestures.detectVerticalDragGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Computer
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.Security
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.Density
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.sp
import androidx.fragment.app.FragmentActivity
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import okhttp3.*
import okio.ByteString
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder
import java.util.concurrent.TimeUnit

private val Forest = Color(0xFF174C3C)
private val ForestDeep = Color(0xFF10382E)
private val Sand = Color(0xFFF0EDE4)
private val Ice = Color(0xFFB9E7F2)
private val Ink = Color(0xFF18352E)
private val http = OkHttpClient.Builder().readTimeout(0, TimeUnit.MILLISECONDS).build()

class MainActivity : FragmentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.statusBarColor = android.graphics.Color.rgb(16,56,46)
        window.navigationBarColor = android.graphics.Color.rgb(16,56,46)
        setContent { val d = LocalDensity.current; CompositionLocalProvider(LocalDensity provides Density(d.density, 1f)) { Z8Home() } }
    }
}

@Composable
private fun Z8Home() {
    var address by remember { mutableStateOf("") }
    var code by remember { mutableStateOf("") }
    var token by remember { mutableStateOf("") }
    var status by remember { mutableStateOf("Not connected") }
    var screen by remember { mutableStateOf<Bitmap?>(null) }
    var page by remember { mutableStateOf("Desktop") }
    var typedText by remember { mutableStateOf("") }
    var clipboardText by remember { mutableStateOf("") }
    var ws by remember { mutableStateOf<WebSocket?>(null) }
    val scope = rememberCoroutineScope()
    val context = LocalContext.current
    val keyboard = LocalSoftwareKeyboardController.current
    val keyboardFocus = remember { FocusRequester() }

    fun command(json: JSONObject) { ws?.send(json.toString()) }
    fun key(vk: Int) = command(JSONObject().put("type", "key").put("keyCode", vk))
    fun sendTextDelta(old: String, new: String) {
        var prefix = 0
        while (prefix < old.length && prefix < new.length && old[prefix] == new[prefix]) prefix++
        val removed = old.length - prefix
        repeat(removed) { key(0x08) }
        if (new.length > prefix) command(JSONObject().put("type", "text").put("text", new.substring(prefix)))
    }

    MaterialTheme(colorScheme = lightColorScheme(primary=Forest, onPrimary=Sand, secondary=Ice, background=Sand, surface=Sand, onSurface=Ink)) {
        Scaffold(
            containerColor = Sand,
            topBar = {
                Column(Modifier.fillMaxWidth().background(Forest).statusBarsPadding().padding(horizontal=20.dp, vertical=16.dp)) {
                    Text("Z8 PC CONNECT", color=Sand, fontSize=23.sp, fontWeight=FontWeight.Bold)
                    Text("Your PC. In your hands.", color=Ice, fontSize=13.sp)
                }
            },
            bottomBar = {
                NavigationBar(containerColor=Forest, contentColor=Sand) {
                    listOf("Desktop","Files","Security","Settings").forEach { item ->
                        NavigationBarItem(selected=page==item, onClick={page=item},
                            icon={ Icon(when(item){"Files"->Icons.Default.Folder;"Security"->Icons.Default.Security;"Settings"->Icons.Default.Settings;else->Icons.Default.Computer}, null) },
                            label={Text(item)}, colors=NavigationBarItemDefaults.colors(selectedIconColor=Forest,selectedTextColor=Sand,indicatorColor=Ice,unselectedIconColor=Sand,unselectedTextColor=Sand))
                    }
                }
            }
        ) { padding ->
            LazyColumn(
                Modifier.fillMaxSize().padding(padding).background(Sand).padding(horizontal=14.dp, vertical=12.dp),
                verticalArrangement=Arrangement.spacedBy(12.dp)
            ) {
                item {
                    Z8Card {
                        Text("PC CONNECTION", color=Forest, fontWeight=FontWeight.Bold)
                        Text(status, color=if(token.isNotBlank()) Forest else Ink)
                        OutlinedTextField(address,{address=it},label={Text("PC address")},singleLine=true,modifier=Modifier.fillMaxWidth())
                        OutlinedTextField(code,{code=it},label={Text("Current six-digit pairing code")},singleLine=true,modifier=Modifier.fillMaxWidth())
                        Button(
                            onClick={ scope.launch {
                                status="Connecting…"
                                try {
                                    val paired = withContext(Dispatchers.IO) {
                                        val c=URL("http://${address.trim()}:8765/pair").openConnection() as HttpURLConnection
                                        c.requestMethod="POST"; c.doOutput=true; c.connectTimeout=5000; c.readTimeout=5000
                                        c.setRequestProperty("Content-Type","application/json")
                                        c.outputStream.use{it.write(JSONObject().put("code",code.trim()).toString().toByteArray())}
                                        if(c.responseCode !in 200..299) error("Pairing rejected (${c.responseCode}). Check the full current code.")
                                        JSONObject(c.inputStream.bufferedReader().readText()).getString("token")
                                    }
                                    token=paired
                                    val request=Request.Builder().url("ws://${address.trim()}:8765/remote?token=$paired").build()
                                    ws=http.newWebSocket(request,object:WebSocketListener(){
                                        override fun onOpen(webSocket:WebSocket,response:Response){status="Connected · live PC screen"}
                                        override fun onMessage(webSocket:WebSocket,bytes:ByteString){BitmapFactory.decodeByteArray(bytes.toByteArray(),0,bytes.size)?.let{screen=it}}
                                        override fun onFailure(webSocket:WebSocket,t:Throwable,response:Response?){status="Connection lost: ${t.message}";token=""}
                                        override fun onClosed(webSocket:WebSocket,code:Int,reason:String){status="Disconnected";token=""}
                                    })
                                } catch(e:Exception){status="Connection failed: ${e.message}"}
                            } }, modifier=Modifier.fillMaxWidth(), colors=ButtonDefaults.buttonColors(containerColor=Forest,contentColor=Sand)
                        ){Text("PAIR AND CONNECT")}
                        OutlinedButton(onClick={ws?.close(1000,"Disconnected");ws=null;token="";screen=null;status="Disconnected"},modifier=Modifier.fillMaxWidth()) { Text("Disconnect",color=Forest) }
                    }
                }

                if(page=="Desktop") {
                    item {
                        Z8Card {
                            Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween,verticalAlignment=Alignment.CenterVertically) {
                                Text("LIVE PC SCREEN",color=Forest,fontWeight=FontWeight.Bold)
                                Text(if(screen!=null) "LIVE" else "WAITING",color=Forest,fontWeight=FontWeight.Bold,fontSize=12.sp)
                            }
                            if(screen!=null) {
                                Box(Modifier.fillMaxWidth().background(Color.Black,RoundedCornerShape(12.dp)).padding(2.dp)) {
                                    Image(
                                        bitmap=screen!!.asImageBitmap(), contentDescription="Entire PC desktop",
                                        modifier=Modifier.fillMaxWidth().pointerInput(screen) {
                                            detectTapGestures(onTap={p ->
                                                val b=screen!!
                                                command(JSONObject().put("type","move").put("x",(p.x/size.width*b.width).toInt()).put("y",(p.y/size.height*b.height).toInt()))
                                                command(JSONObject().put("type","click"))
                                            },onLongPress={command(JSONObject().put("type","rightClick"))})
                                        }.pointerInput(screen) {
                                            detectDragGestures { change,_ ->
                                                change.consume(); val b=screen!!
                                                command(JSONObject().put("type","move").put("x",(change.position.x/size.width*b.width).toInt()).put("y",(change.position.y/size.height*b.height).toInt()))
                                            }
                                        }.pointerInput(screen) {
                                            detectVerticalDragGestures { change,drag ->
                                                change.consume()
                                                command(JSONObject().put("type","scroll").put("delta",(-drag*7).toInt()))
                                            }
                                        }
                                    )
                                }
                                Text("Full desktop fitted to the phone · tap to click · drag to move · swipe vertically to scroll · long-press for right-click",color=Ink,fontSize=12.sp)
                            } else Text("Connect to your PC to see its complete desktop here.",color=Ink)
                            Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(8.dp)) {
                                OutlinedButton(onClick={key(0x5B)},modifier=Modifier.weight(1f)){Text("WINDOWS START",color=Forest)}
                                OutlinedButton(onClick={command(JSONObject().put("type","keyCombo").put("keys",org.json.JSONArray().put(0x11).put(0x43)))},modifier=Modifier.weight(1f)){Text("COPY PC TEXT",color=Forest)}
                            }
                            Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(8.dp)) {
                                OutlinedButton(onClick={key(0x1B)},modifier=Modifier.weight(1f)){Text("ESC",color=Forest)}
                                OutlinedButton(onClick={key(0x0D)},modifier=Modifier.weight(1f)){Text("ENTER",color=Forest)}
                                OutlinedButton(onClick={key(0x09)},modifier=Modifier.weight(1f)){Text("TAB",color=Forest)}
                            }
                        }
                    }
                    item {
                        Z8Card {
                            Text("TYPE DIRECTLY ON YOUR PC",color=Forest,fontWeight=FontWeight.Bold)
                            Text("Tap Open Keyboard, then type normally. Every character is sent straight to the active Windows app as you type; no text box or Send button is shown on the phone.",color=Ink,fontSize=12.sp)
                            BasicTextField(
                                value=typedText,
                                onValueChange={next -> sendTextDelta(typedText,next); typedText=next},
                                modifier=Modifier.size(1.dp).alpha(0f).focusRequester(keyboardFocus),
                                singleLine=false
                            )
                            Row(horizontalArrangement=Arrangement.spacedBy(8.dp)) {
                                Button(onClick={keyboardFocus.requestFocus(); keyboard?.show()},enabled=token.isNotBlank(),colors=ButtonDefaults.buttonColors(containerColor=Forest,contentColor=Sand)){Text("OPEN PC KEYBOARD")}
                                OutlinedButton(onClick={typedText=""}){Text("RESET INPUT",color=Forest)}
                            }
                        }
                    }
                    item {
                        Z8Card {
                            Text("PC CLIPBOARD",color=Forest,fontWeight=FontWeight.Bold)
                            Text("Select text on the PC, tap Copy PC Text, then read it here.",color=Ink,fontSize=12.sp)
                            OutlinedButton(onClick={scope.launch {
                                try {
                                    withContext(Dispatchers.IO) {
                                        val c=URL("http://${address.trim()}:8765/clipboard").openConnection() as HttpURLConnection
                                        c.setRequestProperty("Authorization","Bearer $token"); c.connectTimeout=4000
                                        if(c.responseCode !in 200..299) error("Clipboard request failed")
                                        clipboardText=JSONObject(c.inputStream.bufferedReader().readText()).optString("text")
                                    }
                                } catch(e:Exception){status="Clipboard: ${e.message}"}
                            }},enabled=token.isNotBlank()){Text("READ PC CLIPBOARD",color=Forest)}
                            if(clipboardText.isNotBlank()) Text(clipboardText,color=Ink)
                        }
                    }
                }

                if(page=="Files") item {
                    Z8Card {
                        Text("FILE TRANSFER",color=Forest,fontWeight=FontWeight.Bold)
                        Text("Send a file from your phone to the connected PC.",color=Ink)
                        val picker=androidx.activity.compose.rememberLauncherForActivityResult(androidx.activity.result.contract.ActivityResultContracts.GetContent()){uri->
                            if(uri!=null && token.isNotBlank()) scope.launch {
                                try {
                                    withContext(Dispatchers.IO) {
                                        val name=uri.lastPathSegment?.substringAfterLast('/')?.ifBlank{"upload.bin"} ?: "upload.bin"
                                        val c=URL("http://${address.trim()}:8765/files/upload?name=${URLEncoder.encode(name,"UTF-8")}").openConnection() as HttpURLConnection
                                        c.requestMethod="POST";c.doOutput=true;c.setRequestProperty("Authorization","Bearer $token")
                                        context.contentResolver.openInputStream(uri)!!.use{input->c.outputStream.use{input.copyTo(it)}}
                                        if(c.responseCode !in 200..299) error("Upload failed")
                                    }
                                    status="File sent to PC"
                                }catch(e:Exception){status="File transfer failed: ${e.message}"}
                            }
                        }
                        Button(onClick={picker.launch("*/*")},enabled=token.isNotBlank(),colors=ButtonDefaults.buttonColors(containerColor=Forest,contentColor=Sand)){Text("CHOOSE FILE")}
                    }
                }
                if(page=="Security") item {
                    Z8Card {
                        Text("SECURITY & WINDOWS SIGN-IN",color=Forest,fontWeight=FontWeight.Bold)
                        Text("Z8 can control the currently signed-in Windows desktop after you unlock and pair it. It does not bypass the Windows lock screen or ask for your Microsoft password. Windows Credential Provider integration would require a separately signed and security-reviewed Windows component.",color=Ink)
                        Text("Use a trusted private Wi-Fi network. Do not expose port 8765 to the public internet.",color=Forest,fontWeight=FontWeight.SemiBold)
                    }
                }
                if(page=="Settings") item {
                    Z8Card {
                        Text("SETTINGS",color=Forest,fontWeight=FontWeight.Bold)
                        Text("Theme: Forest Green · Sand · Ice Blue",color=Ink)
                        Text("Connection: Wi-Fi or USB tethered network · TCP 8765",color=Ink)
                        Text("For reliable remote control, keep Windows awake and the Z8 PC app running.",color=Ink)
                    }
                }
            }
        }
    }
}

@Composable
private fun Z8Card(content: @Composable ColumnScope.() -> Unit) {
    Card(
        modifier=Modifier.fillMaxWidth(),
        shape=RoundedCornerShape(20.dp),
        colors=CardDefaults.cardColors(containerColor=Ice),
        elevation=CardDefaults.cardElevation(defaultElevation=5.dp)
    ) {
        Column(Modifier.fillMaxWidth().padding(16.dp),verticalArrangement=Arrangement.spacedBy(10.dp),content=content)
    }
}
