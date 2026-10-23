# The following five lines of boilerplate have to be in your project's
# CMakeLists in this exact order for cmake to work correctly
cmake_minimum_required(VERSION 3.16)

include($ENV{IDF_PATH}/tools/cmake/project.cmake)
set(imx_tuning "${CMAKE_CURRENT_LIST_DIR}/components/esp_cam_sensor_imx/sensors/imx219/cfg/imx219_default.json")
if(NOT EXISTS "${imx_tuning}")
    message(FATAL_ERROR "Missing IMX219 ISP tuning")
endif()
# esp_ipa 2.3 splits its input list on spaces. Stage the tuning file at a
# deterministic temporary path on Windows; configure_file tracks source edits.
if(WIN32)
    file(TO_CMAKE_PATH "$ENV{TEMP}" ipa_temp)
    string(SHA256 ipa_project_id "${CMAKE_CURRENT_LIST_DIR}")
    set(ipa_stage "${ipa_temp}/rov-ipa-${ipa_project_id}")
    if(ipa_stage MATCHES " ")
        message(FATAL_ERROR "Set TEMP to a writable path without spaces for esp_ipa")
    endif()
    file(MAKE_DIRECTORY "${ipa_stage}")
    configure_file("${imx_tuning}" "${ipa_stage}/imx219.json" COPYONLY)
    set(imx_tuning "${ipa_stage}/imx219.json")
endif()
idf_build_set_property(ESP_IPA_JSON_CONFIG_FILE_PATH "${imx_tuning}" APPEND)
set(PROJECT_VER "1.2.1")
project(rov_companion)
