#include "rov.h"
#include <stdatomic.h>
#include <assert.h>
#include "sdkconfig.h"
#include "esp_eth.h"
#include "esp_event.h"
#include "esp_netif.h"
#include "esp_log.h"
#include "lwip/inet.h"
#include "lwip/sockets.h"

static const char *TAG = "rov_eth";
static esp_netif_t *ethernet_netif;
static esp_eth_handle_t ethernet_handle;
static atomic_bool ready;
static atomic_uint generation;

bool rov_ethernet_ready(void) { return atomic_load(&ready); }
uint32_t rov_ethernet_generation(void) { return atomic_load(&generation); }

static void network_event(void *arg, esp_event_base_t base, int32_t id, void *data)
{
    if (base == IP_EVENT && id == IP_EVENT_ETH_GOT_IP) {
        ip_event_got_ip_t *event = data;
        atomic_fetch_add(&generation, 1);
        atomic_store(&ready, true);
        ESP_LOGI(TAG, "Ready: " IPSTR, IP2STR(&event->ip_info.ip));
    } else if ((base == ETH_EVENT &&
                (id == ETHERNET_EVENT_DISCONNECTED || id == ETHERNET_EVENT_STOP)) ||
               (base == IP_EVENT && id == IP_EVENT_ETH_LOST_IP)) {
        atomic_store(&ready, false);
        atomic_fetch_add(&generation, 1);
        ESP_LOGW(TAG, "Ethernet unavailable; queued traffic will expire");
    } else if (base == ETH_EVENT && id == ETHERNET_EVENT_CONNECTED) {
        ESP_LOGI(TAG, "Ethernet link up");
    }
}

void rov_ethernet_start(void)
{
    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());
    esp_netif_config_t net_config = ESP_NETIF_DEFAULT_ETH();
    esp_netif_t *netif = esp_netif_new(&net_config);
    ethernet_netif = netif;
    ESP_ERROR_CHECK(netif ? ESP_OK : ESP_ERR_NO_MEM);
    ESP_ERROR_CHECK(esp_netif_set_hostname(netif, "club-rov"));
#if !CONFIG_ROV_USE_DHCP
    esp_netif_ip_info_t ip = {0};
    ESP_ERROR_CHECK(inet_pton(AF_INET, CONFIG_ROV_STATIC_IP, &ip.ip.addr) == 1 ? ESP_OK : ESP_ERR_INVALID_ARG);
    ESP_ERROR_CHECK(inet_pton(AF_INET, CONFIG_ROV_NETMASK, &ip.netmask.addr) == 1 ? ESP_OK : ESP_ERR_INVALID_ARG);
    ESP_ERROR_CHECK(inet_pton(AF_INET, CONFIG_ROV_GATEWAY, &ip.gw.addr) == 1 ? ESP_OK : ESP_ERR_INVALID_ARG);
    ESP_ERROR_CHECK(esp_netif_dhcpc_stop(netif));
    ESP_ERROR_CHECK(esp_netif_set_ip_info(netif, &ip));
#endif
    // Waveshare IP101 defaults; P4 RMII pin/clock defaults are supplied by IDF.
    eth_esp32_emac_config_t emac = ETH_ESP32_EMAC_DEFAULT_CONFIG();
    emac.smi_gpio.mdc_num = CONFIG_ROV_ETH_MDC_GPIO;
    emac.smi_gpio.mdio_num = CONFIG_ROV_ETH_MDIO_GPIO;
    eth_mac_config_t mac_config = ETH_MAC_DEFAULT_CONFIG();
    eth_phy_config_t phy_config = ETH_PHY_DEFAULT_CONFIG();
    phy_config.phy_addr = CONFIG_ROV_ETH_PHY_ADDR;
    phy_config.reset_gpio_num = CONFIG_ROV_ETH_RESET_GPIO;
    esp_eth_mac_t *mac = esp_eth_mac_new_esp32(&emac, &mac_config);
    esp_eth_phy_t *phy = esp_eth_phy_new_ip101(&phy_config);
    ESP_ERROR_CHECK(mac && phy ? ESP_OK : ESP_ERR_NO_MEM);
    esp_eth_config_t config = ETH_DEFAULT_CONFIG(mac, phy);
    esp_eth_handle_t handle;
    ESP_ERROR_CHECK(esp_eth_driver_install(&config, &handle));
    ethernet_handle = handle;
    esp_eth_netif_glue_handle_t glue = esp_eth_new_netif_glue(handle);
    ESP_ERROR_CHECK(glue ? ESP_OK : ESP_ERR_NO_MEM);
    ESP_ERROR_CHECK(esp_netif_attach(netif, glue));
    ESP_ERROR_CHECK(esp_event_handler_register(ETH_EVENT, ESP_EVENT_ANY_ID, network_event, NULL));
    ESP_ERROR_CHECK(esp_event_handler_register(IP_EVENT, IP_EVENT_ETH_GOT_IP, network_event, NULL));
    ESP_ERROR_CHECK(esp_event_handler_register(IP_EVENT, IP_EVENT_ETH_LOST_IP, network_event, NULL));
    ESP_ERROR_CHECK(esp_eth_start(handle));
}

bool rov_bind_ethernet_socket(int sock)
{
    struct ifreq interface = {0};
    if (!ethernet_netif || esp_netif_get_netif_impl_name(ethernet_netif, interface.ifr_name) != ESP_OK)
        return false;
    return setsockopt(sock, SOL_SOCKET, SO_BINDTODEVICE, &interface, sizeof(interface)) == 0;
}

void rov_ethernet_stop_for_restart(void)
{
    atomic_store(&ready, false);
    atomic_fetch_add(&generation, 1);
    if (ethernet_handle) {
        // IDF 5.5.2's P4 CPU restart does not reset EMAC DMA automatically.
        // Stop the driver before reboot so old receive descriptors cannot corrupt the next boot.
        esp_err_t err = esp_eth_stop(ethernet_handle);
        ESP_LOGI(TAG, "Ethernet stopped before restart: %s", esp_err_to_name(err));
    }
}
