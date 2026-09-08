package com.whiteteam.whitenet

import android.app.ActivityManager
import android.content.Intent
import android.net.Uri
import android.net.TrafficStats
import android.os.BatteryManager
import android.os.Build
import android.os.Bundle
import android.os.Environment
import android.os.Handler
import android.os.Looper
import android.os.StatFs
import android.os.SystemClock
import android.view.View
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.ProgressBar
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.app.AppCompatDelegate
import androidx.appcompat.content.res.AppCompatResources
import androidx.core.os.LocaleListCompat
import java.net.Inet4Address
import java.net.NetworkInterface
import java.net.Socket
import java.util.Locale
import kotlin.concurrent.thread

class MainActivity : AppCompatActivity() {

    private val handler = Handler(Looper.getMainLooper())
    private var prevRx = -1L
    private var prevTx = -1L
    private var online = false
    private var pingBusy = false
    private var lastPingMs: Long? = null

    private lateinit var tabNet: TextView; private lateinit var tabSys: TextView; private lateinit var tabSet: TextView
    private lateinit var panelNet: LinearLayout; private lateinit var panelSys: LinearLayout; private lateinit var panelSet: LinearLayout
    private lateinit var dotOk: ImageView; private lateinit var dotFail: ImageView
    private lateinit var dotWrap: View
    private lateinit var statusTitle: TextView; private lateinit var statusHint: TextView
    private lateinit var ipValue: TextView
    private lateinit var speedDown: TextView; private lateinit var speedUp: TextView
    private lateinit var pingValue: TextView; private lateinit var btnPing: View
    private lateinit var ramBar: ProgressBar; private lateinit var ramDetail: TextView
    private lateinit var diskBar: ProgressBar; private lateinit var diskDetail: TextView
    private lateinit var batBar: ProgressBar; private lateinit var batDetail: TextView
    private lateinit var osValue: TextView; private lateinit var uptimeValue: TextView
    private lateinit var langRu: View; private lateinit var langEn: View

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        bindViews()

        tabNet.setOnClickListener { switchTab(0) }
        tabSys.setOnClickListener { switchTab(1) }
        tabSet.setOnClickListener { switchTab(2) }

        btnPing.setOnClickListener { runPing() }
        dotWrap.setOnClickListener { onDotClick() }
        btnTelegram().setOnClickListener {
            startActivity(Intent(Intent.ACTION_VIEW, Uri.parse("https://t.me/WhteTeam")))
        }
        btnDonate().setOnClickListener {
            startActivity(Intent(Intent.ACTION_VIEW, Uri.parse("https://yoomoney.ru/to/4100119622192067")))
        }
        langRu.setOnClickListener { setLang("ru") }
        langEn.setOnClickListener { setLang("en") }

        osValue.text =
            "Android ${Build.VERSION.RELEASE} (SDK ${Build.VERSION.SDK_INT})\n${Build.MANUFACTURER} ${Build.MODEL}"

        switchTab(0)
        syncLangCards()
        updateLive()
    }

    private fun bindViews() {
        tabNet = findViewById(R.id.tabNet); tabSys = findViewById(R.id.tabSys); tabSet = findViewById(R.id.tabSet)
        panelNet = findViewById(R.id.panelNet); panelSys = findViewById(R.id.panelSys); panelSet = findViewById(R.id.panelSet)
        dotOk = findViewById(R.id.dotOk); dotFail = findViewById(R.id.dotFail); dotWrap = findViewById(R.id.dotWrap)
        statusTitle = findViewById(R.id.statusTitle); statusHint = findViewById(R.id.statusHint)
        ipValue = findViewById(R.id.ipValue)
        speedDown = findViewById(R.id.speedDown); speedUp = findViewById(R.id.speedUp)
        pingValue = findViewById(R.id.pingValue); btnPing = findViewById(R.id.btnPing)
        ramBar = findViewById(R.id.ramBar); ramDetail = findViewById(R.id.ramDetail)
        diskBar = findViewById(R.id.diskBar); diskDetail = findViewById(R.id.diskDetail)
        batBar = findViewById(R.id.batBar); batDetail = findViewById(R.id.batDetail)
        osValue = findViewById(R.id.osValue); uptimeValue = findViewById(R.id.uptimeValue)
        langRu = findViewById(R.id.langRu); langEn = findViewById(R.id.langEn)
    }

    private fun btnTelegram(): View = findViewById(R.id.btnTelegram)

    private fun btnDonate(): View = findViewById(R.id.btnDonate)

    private fun switchTab(i: Int) {
        tabNet.isSelected = i == 0; tabSys.isSelected = i == 1; tabSet.isSelected = i == 2
        tabNet.setTextColor(getColor(if (i == 0) R.color.text_main else R.color.text_dim))
        tabSys.setTextColor(getColor(if (i == 1) R.color.text_main else R.color.text_dim))
        tabSet.setTextColor(getColor(if (i == 2) R.color.text_main else R.color.text_dim))
        panelNet.visibility  = if (i == 0) View.VISIBLE else View.GONE
        panelSys.visibility  = if (i == 1) View.VISIBLE else View.GONE
        panelSet.visibility  = if (i == 2) View.VISIBLE else View.GONE
    }

    private fun setLang(tag: String) {
        AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags(tag))
    }

    private fun syncLangCards() {
        val cur = AppCompatDelegate.getApplicationLocales().toLanguageTags()
        val isEn = cur.startsWith("en")
        val selBg  = AppCompatResources.getDrawable(this, R.drawable.seg_checked)!!
        val cardBg = AppCompatResources.getDrawable(this, R.drawable.card_bg)!!
        langRu.background = if (isEn) cardBg else selBg
        langEn.background = if (isEn) selBg else cardBg
    }

    private val tick = object : Runnable {
        override fun run() {
            updateLive()
            handler.postDelayed(this, 1000)
        }
    }

    override fun onResume() {
        super.onResume()
        prevRx = TrafficStats.getTotalRxBytes(); prevTx = TrafficStats.getTotalTxBytes()
        probeLoop()
        handler.postDelayed(tick, 1000)
    }

    override fun onPause() {
        handler.removeCallbacks(tick)
        super.onPause()
    }

    private fun probeLoop() {
        thread {
            val ok = probeInternet()
            runOnUiThread {
                online = ok
                renderStatus()
            }
            handler.postDelayed({ if (!isFinishing) probeLoop() }, 5000)
        }
    }

    private fun updateLive() {
        val rx = TrafficStats.getTotalRxBytes(); val tx = TrafficStats.getTotalTxBytes()
        if (prevRx >= 0) {
            speedDown.text = fmtSpeed((rx - prevRx).coerceAtLeast(0))
            speedUp.text   = fmtSpeed((tx - prevTx).coerceAtLeast(0))
        }
        prevRx = rx; prevTx = tx

        val am = getSystemService(ACTIVITY_SERVICE) as ActivityManager
        val mi = ActivityManager.MemoryInfo()
        am.getMemoryInfo(mi)
        val totalGb = mi.totalMem / 1073741824.0
        val usedGb  = (mi.totalMem - mi.availMem) / 1073741824.0
        ramBar.progress = ((usedGb / totalGb) * 100).toInt()
        ramDetail.text  = getString(R.string.sp_sub).let { _ ->
            "${fmtGb(usedGb)} / ${fmtGb(totalGb)}"
        }

        try {
            val st = StatFs(Environment.getDataDirectory().path)
            val t = st.totalBytes / 1073741824.0
            val f = st.availableBytes / 1073741824.0
            val u = t - f
            diskBar.progress = ((u / t) * 100).toInt()
            diskDetail.text  = "${fmtGb(u)} / ${fmtGb(t)}"
        } catch (_: Exception) { }

        val bi = registerReceiver(null, android.content.IntentFilter(Intent.ACTION_BATTERY_CHANGED))
        if (bi != null) {
            val level = bi.getIntExtra(BatteryManager.EXTRA_LEVEL, -1)
            val scale = bi.getIntExtra(BatteryManager.EXTRA_SCALE, 100)
            val plugged = bi.getIntExtra(BatteryManager.EXTRA_PLUGGED, 0) != 0
            if (level >= 0) {
                val pct = level * 100 / scale
                batBar.progress = pct
                batDetail.text = "$pct%" + if (plugged) " ⚡" else ""
            }
        }

        uptimeValue.text = uptimeStr()
        ipValue.text = localIp() ?: "—"
        renderStatus()
    }

    private fun renderStatus() {
        dotOk.visibility  = if (online) View.VISIBLE else View.INVISIBLE
        dotFail.visibility = if (online) View.INVISIBLE else View.VISIBLE
        statusTitle.text = getString(if (online) R.string.st_ok else R.string.st_fail)
        statusHint.text  = getString(
            if (online) R.string.net_hint_on else R.string.net_hint_off
        )
    }

    private fun onDotClick() {
        AlertDialog.Builder(this)
            .setTitle(getString(R.string.confirm_net_title))
            .setMessage(getString(R.string.confirm_net_msg))
            .setPositiveButton(getString(R.string.yes)) { _, _ ->
                try {
                    startActivity(Intent(android.provider.Settings.Panel.ACTION_INTERNET_CONNECTIVITY))
                } catch (_: Exception) {
                    startActivity(Intent(android.provider.Settings.ACTION_WIRELESS_SETTINGS))
                }
            }
            .setNegativeButton(getString(R.string.cancel), null)
            .show()
    }

    private fun probeInternet(): Boolean {
        return try {
            val s = Socket()
            s.connect(java.net.InetSocketAddress("8.8.8.8", 53), 1500)
            s.close(); true
        } catch (_: Exception) { false }
    }

    private fun localIp(): String? {
        try {
            val en = NetworkInterface.getNetworkInterfaces() ?: return null
            for (nic in en) {
                if (!nic.isUp || nic.isLoopback) continue
                for (addr in nic.inetAddresses) {
                    if (addr is Inet4Address && !addr.isLoopbackAddress &&
                        (addr.isSiteLocalAddress || addr.address[0] == 192.toByte())
                    ) return addr.hostAddress
                }
            }
        } catch (_: Exception) { }
        return null
    }

    private fun runPing() {
        if (pingBusy) return
        pingBusy = true
        pingValue.text = getString(R.string.p_busy)
        thread {
            val ms = measurePing()
            runOnUiThread {
                lastPingMs = ms
                renderPing(ms)
                pingBusy = false
            }
        }
    }

    private fun renderPing(ms: Long?) {
        pingValue.text = when {
            ms == null -> getString(R.string.p_hint)
            ms < 0     -> getString(R.string.p_fail)
            else       -> "$ms " + getString(R.string.ms_unit)
        }
    }

    private fun measurePing(): Long {
        return try {
            val start = SystemClock.elapsedRealtime()
            val proc = ProcessBuilder("/system/bin/ping", "-c", "1", "-W", "3", "8.8.8.8")
                .redirectErrorStream(true).start()
            val out = proc.inputStream.bufferedReader().readText()
            proc.waitFor()
            Regex("time[=<](\\d+(?:\\.\\d+)?)").find(out)?.groupValues?.get(1)?.toDoubleOrNull()
                ?.toLong() ?: (SystemClock.elapsedRealtime() - start).let { if (proc.exitValue() == 0) it else -1L }
        } catch (_: Exception) { -1L }
    }

    private fun fmtSpeed(bps: Long): String = when {
        bps < 1024 -> "$bps ${getString(R.string.unit_b)}"
        bps < 1024 * 1024 -> String.format(Locale.US, "%.1f %s", bps / 1024.0, getString(R.string.unit_kb))
        else -> String.format(Locale.US, "%.2f %s", bps / 1048576.0, getString(R.string.unit_mb))
    }

    private fun fmtGb(gb: Double): String =
        if (gb >= 1) String.format(Locale.US, "%.1f GB", gb)
        else "${(gb * 1024).toInt()} MB"

    private fun uptimeStr(): String {
        val t = SystemClock.elapsedRealtime() / 1000
        val d = t / 86400; val h = t % 86400 / 3600; val m = t % 3600 / 60
        return if (d > 0) "${d}d ${h}h ${m}min" else "${h}h ${m}min"
    }

    @Deprecated("Deprecated in Java")
    override fun onBackPressed() {
        moveTaskToBack(true)
    }
}
